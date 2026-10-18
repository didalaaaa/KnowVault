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
using Volo.Abp.Domain.Repositories;
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

		private readonly IRagService _ragService;
		private readonly IRepository<Conversation, Guid> _conversationRepository;
		private readonly IRepository<Message, Guid> _messageRepository;
		private readonly IUnitOfWorkManager _unitOfWorkManager;
		private readonly IOptions<AiOptions> _aiOptions;

		/// <summary>
		/// 构造函数
		/// </summary>
		/// <param name="ragService">RAG 编排入口</param>
		/// <param name="conversationRepository">会话仓储</param>
		/// <param name="messageRepository">消息仓储</param>
		/// <param name="unitOfWorkManager">工作单元管理器</param>
		/// <param name="aiOptions">AI 配置（历史轮数）</param>
		public QaStreamService(
			IRagService ragService,
			IRepository<Conversation, Guid> conversationRepository,
			IRepository<Message, Guid> messageRepository,
			IUnitOfWorkManager unitOfWorkManager,
			IOptions<AiOptions> aiOptions)
		{
			_ragService = ragService;
			_conversationRepository = conversationRepository;
			_messageRepository = messageRepository;
			_unitOfWorkManager = unitOfWorkManager;
			_aiOptions = aiOptions;
		}

		/// <inheritdoc />
		public async IAsyncEnumerable<QaStreamEventDto> AskStreamAsync(AskQuestionInput input, [EnumeratorCancellation] CancellationToken cancellationToken = default)
		{
			using var activity = ActivitySource.StartActivity("qa.ask");
			activity?.SetTag("qa.knowledge_base_id", input.KnowledgeBaseId);
			activity?.SetTag("qa.question_length", input.Question.Length);

			Conversation conversation;
			IReadOnlyList<LlmChatMessage> history;

			// 第一段 UoW：解析会话、读历史、落用户消息，随即释放，不跨模型生成持有连接
			using (var unitOfWork = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: false))
			{
				conversation = await ResolveConversationAsync(input, cancellationToken);
				history = await LoadHistoryAsync(conversation.Id, cancellationToken);
				await _messageRepository.InsertAsync(new Message(GuidGenerator.Create(), conversation.Id, MessageRoleEnum.User, input.Question), autoSave: true, cancellationToken);
				await unitOfWork.CompleteAsync(cancellationToken);
			}

			activity?.SetTag("qa.conversation_id", conversation.Id);

			// RAG 编排：知识库缺省回落默认知识库；租户 null 归一化为 Guid.Empty（与 Qdrant payload 一致）
			var streamResult = await _ragService.AnswerStreamAsync(new RagRequest
			{
				KnowledgeBaseId = input.KnowledgeBaseId ?? KnowVaultConsts.DefaultKnowledgeBaseId,
				TenantId = CurrentTenant.Id ?? Guid.Empty,
				ConversationId = conversation.Id,
				Question = input.Question,
				TopK = input.TopK ?? 0,
				History = history
			}, cancellationToken);

			var citations = streamResult.Chunks.Select(chunk => new RagCitationDto
			{
				DocumentId = chunk.DocumentId,
				Title = chunk.DocumentTitle,
				ChunkIndex = chunk.ChunkIndex,
				Score = chunk.Score
			}).ToList();

			yield return new QaStreamEventDto
			{
				Kind = QaStreamEventKindEnum.Started,
				ConversationId = conversation.Id,
				Citations = citations
			};

			var answer = new StringBuilder();
			await foreach (var delta in streamResult.Deltas.WithCancellation(cancellationToken))
			{
				answer.Append(delta);
				yield return new QaStreamEventDto { Kind = QaStreamEventKindEnum.Delta, Delta = delta };
			}

			// 第二段 UoW：助手消息落库；客户端中途断开时迭代器在这里之前就退出，半截回答不入库
			using (var unitOfWork = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: false))
			{
				await _messageRepository.InsertAsync(
					new Message(
						GuidGenerator.Create(),
						conversation.Id,
						MessageRoleEnum.Assistant,
						answer.ToString(),
						citationsJson: JsonSerializer.Serialize(citations)),
					autoSave: true,
					cancellationToken: cancellationToken);
				await unitOfWork.CompleteAsync(cancellationToken);
			}

			yield return new QaStreamEventDto { Kind = QaStreamEventKindEnum.Completed, ConversationId = conversation.Id };
		}

		/// <summary>
		/// 取已有会话，或按问题前 128 字符为标题新建会话
		/// </summary>
		/// <param name="input">提问参数</param>
		/// <param name="cancellationToken">取消令牌</param>
		/// <returns>会话实体</returns>
		private async Task<Conversation> ResolveConversationAsync(AskQuestionInput input, CancellationToken cancellationToken)
		{
			if (input.ConversationId.HasValue) return await _conversationRepository.GetAsync(input.ConversationId.Value, cancellationToken: cancellationToken);

			return await _conversationRepository.InsertAsync(
				new Conversation(
					GuidGenerator.Create(),
					input.Question.Length <= 128 ? input.Question : input.Question[..128]),
				autoSave: true,
				cancellationToken: cancellationToken);
		}

		/// <summary>
		/// 读取最近若干轮历史（时间升序）——必须在插入本轮用户消息之前调用
		/// </summary>
		/// <param name="conversationId">会话 ID</param>
		/// <param name="cancellationToken">取消令牌</param>
		/// <returns>历史消息（不含本轮提问）</returns>
		private async Task<IReadOnlyList<LlmChatMessage>> LoadHistoryAsync(Guid conversationId, CancellationToken cancellationToken)
		{
			var maxMessages = Math.Max(_aiOptions.Value.HistoryMaxMessages, 0);
			if (maxMessages == 0) return [];

			// 会话消息量小，取全量后在内存里排序；P3 消息量上来再下推到 SQL
			var messages = await _messageRepository.GetListAsync(
				message => message.ConversationId == conversationId,
				cancellationToken: cancellationToken);

			return [.. messages
				.OrderByDescending(message => message.CreationTime)
				.Take(maxMessages)
				.OrderBy(message => message.CreationTime)
				.Select(message => new LlmChatMessage { Role = message.Role, Content = message.Content })];
		}
	}
}
