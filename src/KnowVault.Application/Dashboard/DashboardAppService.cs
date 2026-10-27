using KnowVault.Application.Contracts;
using KnowVault.Domain;
using KnowVault.Domain.Shared;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Timing;

namespace KnowVault.Application
{
	/// <summary>
	/// 仪表盘统计：作品规模下直接取列表后内存聚合，P5 数据量上来再下推 SQL
	/// </summary>
	[Authorize]
	public class DashboardAppService : KnowVaultAppService, IDashboardAppService
	{
		private readonly IRepository<Document, Guid> _documentRepository;
		private readonly IRepository<Conversation, Guid> _conversationRepository;
		private readonly IRepository<Message, Guid> _messageRepository;
		private readonly IRepository<AiUsageRecord, Guid> _usageRepository;
		private readonly IRepository<KnowledgeBase, Guid> _knowledgeBaseRepository;
		private readonly KnowledgeBaseAccessChecker _accessChecker;
		private readonly IClock _clock;

		/// <summary>
		/// 构造函数
		/// </summary>
		/// <param name="documentRepository">文档仓储</param>
		/// <param name="conversationRepository">会话仓储</param>
		/// <param name="messageRepository">消息仓储</param>
		/// <param name="usageRepository">AI 用量记录仓储</param>
		/// <param name="knowledgeBaseRepository">知识库仓储（取用量表里的库名）</param>
		/// <param name="accessChecker">知识库访问检查器</param>
		/// <param name="clock">时钟（按本地日期切分今日与趋势）</param>
		public DashboardAppService(
			IRepository<Document, Guid> documentRepository,
			IRepository<Conversation, Guid> conversationRepository,
			IRepository<Message, Guid> messageRepository,
			IRepository<AiUsageRecord, Guid> usageRepository,
			IRepository<KnowledgeBase, Guid> knowledgeBaseRepository,
			KnowledgeBaseAccessChecker accessChecker,
			IClock clock)
		{
			_documentRepository = documentRepository;
			_conversationRepository = conversationRepository;
			_messageRepository = messageRepository;
			_usageRepository = usageRepository;
			_knowledgeBaseRepository = knowledgeBaseRepository;
			_accessChecker = accessChecker;
			_clock = clock;
		}

		/// <inheritdoc />
		public async Task<DashboardDto> GetAsync(CancellationToken cancellationToken)
		{
			var accessibleIds = await _accessChecker.GetAccessibleIdsAsync(cancellationToken);
			var documents = await _documentRepository.GetListAsync(
				document => accessibleIds.Contains(document.KnowledgeBaseId),
				cancellationToken: cancellationToken);

			var userId = CurrentUser.Id;
			IReadOnlyList<Conversation> conversations = userId is null
				? []
				: await _conversationRepository.GetListAsync(
					conversation => conversation.CreatorId == userId,
					cancellationToken: cancellationToken);

			var conversationIds = conversations.Select(conversation => conversation.Id).ToHashSet();
			IReadOnlyList<Message> messages = conversationIds.Count == 0
				? []
				: await _messageRepository.GetListAsync(
					message => conversationIds.Contains(message.ConversationId),
					cancellationToken: cancellationToken);

			var today = _clock.Now.Date;
			var trendStart = today.AddDays(-6);

			// 用量只统计本人问答与可访问库的摄取，旁路记录写入失败也不会影响这里的展示
			var usageRecords = await _usageRepository.GetListAsync(
				record => record.CreationTime >= trendStart,
				cancellationToken: cancellationToken);
			var visibleUsages = usageRecords
				.Where(record =>
					(userId is not null && record.AskId.HasValue && record.CreatorId == userId)
					|| (record.Operation == AiOperationEnum.Embedding
						&& record.DocumentId.HasValue
						&& record.KnowledgeBaseId.HasValue
						&& accessibleIds.Contains(record.KnowledgeBaseId.Value)))
				.ToList();

			var usageKnowledgeBaseIds = visibleUsages
				.Where(record => record.KnowledgeBaseId.HasValue)
				.Select(record => record.KnowledgeBaseId!.Value)
				.Distinct()
				.ToList();
			IReadOnlyDictionary<Guid, string> knowledgeBaseNames = usageKnowledgeBaseIds.Count == 0
				? new Dictionary<Guid, string>()
				: (await _knowledgeBaseRepository.GetListAsync(
					knowledgeBase => usageKnowledgeBaseIds.Contains(knowledgeBase.Id),
					cancellationToken: cancellationToken))
					.ToDictionary(knowledgeBase => knowledgeBase.Id, knowledgeBase => knowledgeBase.Name);

			var questions = messages.Where(message => message.Role == MessageRoleEnum.User).ToList();
			var latencySamples = messages
				.Where(message => message.Role == MessageRoleEnum.Assistant
					&& message.LatencyMs.HasValue
					&& message.CreationTime >= trendStart)
				.Select(message => message.LatencyMs!.Value)
				.ToList();

			return new DashboardDto
			{
				KnowledgeBaseCount = accessibleIds.Count,
				DocumentCount = documents.Count,
				ReadyDocumentCount = documents.Count(document => document.Status == DocumentStatusEnum.Ready),
				FailedDocumentCount = documents.Count(document => document.Status == DocumentStatusEnum.Failed),
				ConversationCount = conversations.Count,
				QuestionCount = questions.Count,
				TodayQuestionCount = questions.Count(question => question.CreationTime >= today),
				AverageLatencyMs = latencySamples.Count == 0 ? 0 : latencySamples.Average(),
				Last7DayQuestions = [.. Enumerable.Range(0, 7).Select(offset =>
				{
					var date = trendStart.AddDays(offset);
					return new DashboardTrendPointDto
					{
						Date = date,
						Count = questions.Count(question => question.CreationTime.Date == date)
					};
				})],
				Usage = AiUsageAggregator.Aggregate(visibleUsages, knowledgeBaseNames, today)
			};
		}
	}
}
