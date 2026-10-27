using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using KnowVault.Ai;
using KnowVault.Application.Contracts;
using KnowVault.Domain;
using KnowVault.Domain.Shared;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace KnowVault.Application
{
	/// <summary>
	/// 流式问答编排：会话与消息落库都在这里，REST 与 SignalR 共用同一条实现
	/// </summary>
	/// <remarks>
	/// 流式方法返回 <see cref="IAsyncEnumerable{T}"/>，环境 UoW 会在迭代开始前就被释放，
	/// 因此两段落库各自用显式 UoW（<c>requiresNew: true</c>），生成过程不持有数据库连接
	/// </remarks>
	[RemoteService(IsEnabled = false)]
	public class QaStreamService : KnowVaultAppService, IQaStreamService
	{
		private static readonly ActivitySource ActivitySource = new(KnowVaultTelemetry.ActivitySourceName);
		private const int CitationSnippetLength = 300;

		private readonly IRagService _ragService;
		private readonly IRepository<Conversation, Guid> _conversationRepository;
		private readonly IRepository<Message, Guid> _messageRepository;
		private readonly IRepository<KnowledgeBase, Guid> _knowledgeBaseRepository;
		private readonly IUnitOfWorkManager _unitOfWorkManager;
		private readonly IOptions<AiOptions> _aiOptions;
		private readonly KnowledgeBaseScopeResolver _scopeResolver;
		private readonly IClock _clock;

		/// <summary>
		/// 构造函数
		/// </summary>
		/// <param name="ragService">RAG 编排入口</param>
		/// <param name="conversationRepository">会话仓储</param>
		/// <param name="messageRepository">消息仓储</param>
		/// <param name="knowledgeBaseRepository">知识库仓储（读取切片与检索设置）</param>
		/// <param name="unitOfWorkManager">工作单元管理器</param>
		/// <param name="aiOptions">AI 配置（历史轮数）</param>
		/// <param name="scopeResolver">知识库范围解析与权限检查</param>
		/// <param name="clock">时钟（记录会话活动时间）</param>
		public QaStreamService(
			IRagService ragService,
			IRepository<Conversation, Guid> conversationRepository,
			IRepository<Message, Guid> messageRepository,
			IRepository<KnowledgeBase, Guid> knowledgeBaseRepository,
			IUnitOfWorkManager unitOfWorkManager,
			IOptions<AiOptions> aiOptions,
			KnowledgeBaseScopeResolver scopeResolver,
			IClock clock)
		{
			_ragService = ragService;
			_conversationRepository = conversationRepository;
			_messageRepository = messageRepository;
			_knowledgeBaseRepository = knowledgeBaseRepository;
			_unitOfWorkManager = unitOfWorkManager;
			_aiOptions = aiOptions;
			_scopeResolver = scopeResolver;
			_clock = clock;
		}

		/// <inheritdoc />
		public async IAsyncEnumerable<QaStreamEventDto> AskStreamAsync(AskQuestionInput input, [EnumeratorCancellation] CancellationToken cancellationToken = default)
		{
			// 先解析范围并逐个校验 Viewer 权限，无权知识库直接报错而不是静默丢弃
			var scope = await _scopeResolver.ResolveAsync(input.KnowledgeBaseIds, input.AllKnowledgeBases, cancellationToken);
			var singleKnowledgeBaseId = !scope.All && scope.Ids.Count == 1 ? scope.Ids[0] : (Guid?)null;

			// 重新生成要沿用当前会话与上一轮问题，没有会话 ID 就无从定位
			if (input.Regenerate && !input.ConversationId.HasValue) throw new UserFriendlyException("重新生成需要指定会话");

			// 同一次问答的各阶段记录共用一个 AskId（用量记录在 T5 落库）
			var askId = GuidGenerator.Create();

			var stopwatch = Stopwatch.StartNew();
			using var activity = ActivitySource.StartActivity("qa.ask");
			activity?.SetTag("qa.ask_id", askId);
			activity?.SetTag("qa.knowledge_base_count", scope.Ids.Count);
			activity?.SetTag("qa.knowledge_base_all", scope.All);
			activity?.SetTag("qa.question_length", input.Question.Length);
			activity?.SetTag("qa.regenerate", input.Regenerate);

			Conversation conversation;
			IReadOnlyList<LlmChatMessage> history;
			KnowledgeBase? knowledgeBase;
			Guid? userMessageId = null;

			// 第一段 UoW：解析会话、读历史、落用户消息，随即释放，不跨模型生成持有连接
			using (var unitOfWork = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: false))
			{
				conversation = await ResolveConversationAsync(input, scope, cancellationToken);
				history = await LoadHistoryAsync(conversation.Id, input.Regenerate, cancellationToken);
				// 只有单库提问才沿用该库的 TopK 与自定义提示词；多库/全部使用全局配置与内置模板
				knowledgeBase = singleKnowledgeBaseId.HasValue
					? await _knowledgeBaseRepository.FindAsync(singleKnowledgeBaseId.Value, cancellationToken: cancellationToken)
					: null;

				// 重新生成不重复插入用户消息，只重新回答上一轮问题
				if (!input.Regenerate)
				{
					var userMessage = new Message(GuidGenerator.Create(), conversation.Id, MessageRoleEnum.User, input.Question);
					await _messageRepository.InsertAsync(userMessage, autoSave: true, cancellationToken);
					userMessageId = userMessage.Id;
				}

				await unitOfWork.CompleteAsync(cancellationToken);
			}

			activity?.SetTag("qa.conversation_id", conversation.Id);

			// RAG 编排：租户 null 归一化为 Guid.Empty（与 Qdrant payload 一致）
			var streamResult = await _ragService.AnswerStreamAsync(new RagRequest
			{
				KnowledgeBaseIds = scope.Ids,
				TenantId = CurrentTenant.Id ?? Guid.Empty,
				ConversationId = conversation.Id,
				Question = input.Question,
				// 请求参数优先，其次单库设置，最后交给全局配置
				TopK = input.TopK ?? knowledgeBase?.TopK ?? 0,
				SystemPromptOverride = knowledgeBase?.SystemPrompt,
				History = history,
				HybridEnabled = input.HybridEnabled,
				RewriteEnabled = input.RewriteEnabled
			}, cancellationToken);

			var citations = streamResult.Chunks.Select(chunk => new RagCitationDto
			{
				DocumentId = chunk.DocumentId,
				KnowledgeBaseId = chunk.KnowledgeBaseId,
				KnowledgeBaseName = scope.Names.GetValueOrDefault(chunk.KnowledgeBaseId),
				Title = chunk.DocumentTitle,
				ChunkIndex = chunk.ChunkIndex,
				Score = chunk.Score,
				Snippet = chunk.Content.Length <= CitationSnippetLength ? chunk.Content : chunk.Content[..CitationSnippetLength],
				HeadingPath = string.IsNullOrWhiteSpace(chunk.HeadingPath) ? null : chunk.HeadingPath
			}).ToList();

			yield return new QaStreamEventDto
			{
				Kind = QaStreamEventKindEnum.Started,
				ConversationId = conversation.Id,
				UserMessageId = userMessageId,
				Citations = citations,
				KnowledgeBaseIds = scope.Ids,
				HybridEnabled = input.HybridEnabled ?? _aiOptions.Value.HybridEnabled,
				RewriteQueries = streamResult.RewriteQueries
			};

			var answer = new StringBuilder();
			await foreach (var chunk in streamResult.Deltas.WithCancellation(cancellationToken))
			{
				if (string.IsNullOrEmpty(chunk.Delta)) continue;

				answer.Append(chunk.Delta);
				yield return new QaStreamEventDto { Kind = QaStreamEventKindEnum.Delta, Delta = chunk.Delta };
			}

			stopwatch.Stop();
			var latencyMs = (int)Math.Min(stopwatch.ElapsedMilliseconds, int.MaxValue);

			// 第二段 UoW：助手消息落库并刷新会话活动时间；客户端中途断开时迭代器在这里之前就退出，半截回答不入库
			using (var unitOfWork = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: false))
			{
				var assistantMessage = new Message(
					GuidGenerator.Create(),
					conversation.Id,
					MessageRoleEnum.Assistant,
					answer.ToString(),
					citationsJson: JsonSerializer.Serialize(citations));
				assistantMessage.SetLatency(latencyMs);

				await _messageRepository.InsertAsync(assistantMessage, autoSave: true, cancellationToken: cancellationToken);

				conversation.Touch(_clock.Now);
				await _conversationRepository.UpdateAsync(conversation, autoSave: true, cancellationToken: cancellationToken);
				await unitOfWork.CompleteAsync(cancellationToken);

				yield return new QaStreamEventDto
				{
					Kind = QaStreamEventKindEnum.Completed,
					ConversationId = conversation.Id,
					AssistantMessageId = assistantMessage.Id,
					LatencyMs = latencyMs
				};
			}
		}

		/// <summary>
		/// 取已有会话，或按问题前 128 字符为标题新建会话，并写入本次提问范围
		/// </summary>
		/// <param name="input">提问参数</param>
		/// <param name="scope">本次解析后的知识库范围</param>
		/// <param name="cancellationToken">取消令牌</param>
		/// <returns>会话实体</returns>
		private async Task<Conversation> ResolveConversationAsync(AskQuestionInput input, KnowledgeBaseScope scope, CancellationToken cancellationToken)
		{
			if (input.ConversationId.HasValue)
			{
				var existing = await _conversationRepository.GetAsync(input.ConversationId.Value, cancellationToken: cancellationToken);
				if (existing.CreatorId != CurrentUser.Id) throw new AbpAuthorizationException("无权访问该会话");

				// 追问可能更换范围，主库一并更新以保持会话列表归组正确
				existing.UpdateScope(scope.PrimaryId, scope.Ids, scope.All);
				await _conversationRepository.UpdateAsync(existing, autoSave: true, cancellationToken: cancellationToken);
				return existing;
			}

			var conversation = new Conversation(
				GuidGenerator.Create(),
				input.Question.Length <= 128 ? input.Question : input.Question[..128],
				knowledgeBaseId: scope.PrimaryId);
			conversation.UpdateScope(scope.PrimaryId, scope.Ids, scope.All);

			return await _conversationRepository.InsertAsync(
				conversation,
				autoSave: true,
				cancellationToken: cancellationToken);
		}

		/// <summary>
		/// 读取最近若干轮历史（时间升序）——必须在插入本轮用户消息之前调用
		/// </summary>
		/// <param name="conversationId">会话 ID</param>
		/// <param name="excludeLastTurn">重新生成时剔除最后一轮用户提问及其后的助手回答</param>
		/// <param name="cancellationToken">取消令牌</param>
		/// <returns>历史消息（不含本轮提问）</returns>
		private async Task<IReadOnlyList<LlmChatMessage>> LoadHistoryAsync(Guid conversationId, bool excludeLastTurn, CancellationToken cancellationToken)
		{
			var maxMessages = Math.Max(_aiOptions.Value.HistoryMaxMessages, 0);
			if (maxMessages == 0) return [];

			// 会话消息量小，取全量后在内存里排序；P3 消息量上来再下推到 SQL
			var messages = await _messageRepository.GetListAsync(
				message => message.ConversationId == conversationId,
				cancellationToken: cancellationToken);

			var ordered = messages.OrderBy(message => message.CreationTime).ToList();
			if (excludeLastTurn)
			{
				// 旧回答也要一起剔除：只删提问会让模型把上一版回答当成已经给出的上下文
				var lastUserIndex = ordered.FindLastIndex(message => message.Role == MessageRoleEnum.User);
				if (lastUserIndex >= 0) ordered.RemoveRange(lastUserIndex, ordered.Count - lastUserIndex);
			}

			return [.. ordered
				.TakeLast(maxMessages)
				.Select(message => new LlmChatMessage { Role = message.Role, Content = message.Content })];
		}
	}
}
