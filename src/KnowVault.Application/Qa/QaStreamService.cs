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
		private readonly KnowledgeBaseAccessChecker _accessChecker;
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
		/// <param name="accessChecker">知识库访问检查器</param>
		/// <param name="clock">时钟（记录会话活动时间）</param>
		public QaStreamService(
			IRagService ragService,
			IRepository<Conversation, Guid> conversationRepository,
			IRepository<Message, Guid> messageRepository,
			IRepository<KnowledgeBase, Guid> knowledgeBaseRepository,
			IUnitOfWorkManager unitOfWorkManager,
			IOptions<AiOptions> aiOptions,
			KnowledgeBaseAccessChecker accessChecker,
			IClock clock)
		{
			_ragService = ragService;
			_conversationRepository = conversationRepository;
			_messageRepository = messageRepository;
			_knowledgeBaseRepository = knowledgeBaseRepository;
			_unitOfWorkManager = unitOfWorkManager;
			_aiOptions = aiOptions;
			_accessChecker = accessChecker;
			_clock = clock;
		}

		/// <inheritdoc />
		public async IAsyncEnumerable<QaStreamEventDto> AskStreamAsync(AskQuestionInput input, [EnumeratorCancellation] CancellationToken cancellationToken = default)
		{
			var knowledgeBaseId = input.KnowledgeBaseId ?? KnowVaultConsts.DefaultKnowledgeBaseId;
			await _accessChecker.EnsureAsync(knowledgeBaseId, KnowledgeBaseRoleEnum.Viewer, cancellationToken);

			// 重新生成要沿用当前会话与上一轮问题，没有会话 ID 就无从定位
			if (input.Regenerate && !input.ConversationId.HasValue) throw new UserFriendlyException("重新生成需要指定会话");

			var stopwatch = Stopwatch.StartNew();
			using var activity = ActivitySource.StartActivity("qa.ask");
			activity?.SetTag("qa.knowledge_base_id", input.KnowledgeBaseId);
			activity?.SetTag("qa.question_length", input.Question.Length);
			activity?.SetTag("qa.regenerate", input.Regenerate);

			Conversation conversation;
			IReadOnlyList<LlmChatMessage> history;
			KnowledgeBase? knowledgeBase;
			Guid? userMessageId = null;

			// 第一段 UoW：解析会话、读历史、落用户消息，随即释放，不跨模型生成持有连接
			using (var unitOfWork = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: false))
			{
				conversation = await ResolveConversationAsync(input, cancellationToken);
				history = await LoadHistoryAsync(conversation.Id, input.Regenerate, cancellationToken);
				knowledgeBase = await _knowledgeBaseRepository.FindAsync(knowledgeBaseId, cancellationToken: cancellationToken);

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

			// RAG 编排：知识库缺省回落默认知识库；租户 null 归一化为 Guid.Empty（与 Qdrant payload 一致）
			var streamResult = await _ragService.AnswerStreamAsync(new RagRequest
			{
				KnowledgeBaseId = knowledgeBaseId,
				TenantId = CurrentTenant.Id ?? Guid.Empty,
				ConversationId = conversation.Id,
				Question = input.Question,
				// 请求参数优先，其次知识库设置，最后交给全局配置
				TopK = input.TopK ?? knowledgeBase?.TopK ?? 0,
				SystemPromptOverride = knowledgeBase?.SystemPrompt,
				History = history
			}, cancellationToken);

			var citations = streamResult.Chunks.Select(chunk => new RagCitationDto
			{
				DocumentId = chunk.DocumentId,
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
				Citations = citations
			};

			var answer = new StringBuilder();
			await foreach (var delta in streamResult.Deltas.WithCancellation(cancellationToken))
			{
				answer.Append(delta);
				yield return new QaStreamEventDto { Kind = QaStreamEventKindEnum.Delta, Delta = delta };
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
		/// 取已有会话，或按问题前 128 字符为标题新建会话
		/// </summary>
		/// <param name="input">提问参数</param>
		/// <param name="cancellationToken">取消令牌</param>
		/// <returns>会话实体</returns>
		private async Task<Conversation> ResolveConversationAsync(AskQuestionInput input, CancellationToken cancellationToken)
		{
			if (input.ConversationId.HasValue)
			{
				var existing = await _conversationRepository.GetAsync(input.ConversationId.Value, cancellationToken: cancellationToken);
				if (existing.CreatorId != CurrentUser.Id) throw new AbpAuthorizationException("无权访问该会话");

				return existing;
			}

			return await _conversationRepository.InsertAsync(
				new Conversation(
					GuidGenerator.Create(),
					input.Question.Length <= 128 ? input.Question : input.Question[..128],
					knowledgeBaseId: input.KnowledgeBaseId ?? KnowVaultConsts.DefaultKnowledgeBaseId),
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
