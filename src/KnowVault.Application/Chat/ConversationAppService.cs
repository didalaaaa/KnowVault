using System.Text.Json;
using KnowVault.Application.Contracts;
using KnowVault.Domain;
using KnowVault.Domain.Shared;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;

namespace KnowVault.Application
{
	/// <summary>
	/// 会话应用服务：只允许访问本人创建的会话
	/// </summary>
	[Authorize]
	public class ConversationAppService : KnowVaultAppService, IConversationAppService
	{
		private readonly IRepository<Conversation, Guid> _conversationRepository;
		private readonly IRepository<Message, Guid> _messageRepository;
		private readonly IRepository<MessageFeedback, Guid> _feedbackRepository;

		/// <summary>
		/// 构造函数
		/// </summary>
		/// <param name="conversationRepository">会话仓储</param>
		/// <param name="messageRepository">消息仓储</param>
		/// <param name="feedbackRepository">消息反馈仓储</param>
		public ConversationAppService(
			IRepository<Conversation, Guid> conversationRepository,
			IRepository<Message, Guid> messageRepository,
			IRepository<MessageFeedback, Guid> feedbackRepository)
		{
			_conversationRepository = conversationRepository;
			_messageRepository = messageRepository;
			_feedbackRepository = feedbackRepository;
		}

		/// <inheritdoc />
		public async Task<List<ConversationDto>> GetListAsync(Guid? knowledgeBaseId, CancellationToken cancellationToken)
		{
			var userId = CurrentUser.Id ?? throw new AbpAuthorizationException("请先登录");

			var conversations = await _conversationRepository.GetListAsync(
				x => x.CreatorId == userId && (knowledgeBaseId == null || x.KnowledgeBaseId == knowledgeBaseId),
				cancellationToken: cancellationToken);

			// 会话量小，最近活动时间在内存里排序；P5 会话多了再下推到 SQL
			return [.. conversations
				.OrderByDescending(x => x.LastMessageAt ?? x.CreationTime)
				.Select(MapToDto)];
		}

		/// <inheritdoc />
		public async Task<List<ChatMessageDto>> GetMessagesAsync(Guid id, CancellationToken cancellationToken)
		{
			var conversation = await GetOwnedConversationAsync(id, cancellationToken);

			var messages = await _messageRepository.GetListAsync(
				x => x.ConversationId == conversation.Id,
				cancellationToken: cancellationToken);

			// 只回填当前用户自己的反馈，别人的评价不暴露
			var userId = CurrentUser.Id;
			var messageIds = messages.Select(x => x.Id).ToList();
			var feedbacks = userId == null || messageIds.Count == 0
				? []
				: await _feedbackRepository.GetListAsync(
					x => x.UserId == userId.Value && messageIds.Contains(x.MessageId),
					cancellationToken: cancellationToken);
			// 值类型字典的默认值是 Like(0)，缺反馈时会误显示为已点赞，显式提升为可空
			var feedbackByMessage = feedbacks.ToDictionary(x => x.MessageId, x => (FeedbackRatingEnum?)x.Rating);

			return [.. messages
				.OrderBy(x => x.CreationTime)
				.Select(x => new ChatMessageDto
				{
					Id = x.Id,
					Role = x.Role,
					Content = x.Content,
					Citations = ParseCitations(x.CitationsJson),
					CreationTime = x.CreationTime,
					Feedback = feedbackByMessage.GetValueOrDefault(x.Id),
					LatencyMs = x.LatencyMs
				})];
		}

		/// <inheritdoc />
		public async Task<ConversationDto> UpdateAsync(RenameConversationInput input, CancellationToken cancellationToken)
		{
			var conversation = await GetOwnedConversationAsync(input.Id, cancellationToken);

			var title = input.Title.Trim();
			if (title.Length == 0) throw new UserFriendlyException("会话标题不能为空");

			conversation.Rename(title);
			await _conversationRepository.UpdateAsync(conversation, autoSave: true, cancellationToken: cancellationToken);

			return MapToDto(conversation);
		}

		/// <inheritdoc />
		public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
		{
			var conversation = await GetOwnedConversationAsync(id, cancellationToken);

			// 仓储的批量删除只接受主键集合
			var messageIds = (await _messageRepository.GetListAsync(x => x.ConversationId == id, cancellationToken: cancellationToken))
				.Select(x => x.Id)
				.ToList();

			if (messageIds.Count > 0)
			{
				// 会话删除要连带清掉这些消息上的反馈，避免留下孤儿行
				var feedbackIds = (await _feedbackRepository.GetListAsync(
					x => messageIds.Contains(x.MessageId),
					cancellationToken: cancellationToken)).Select(x => x.Id).ToList();
				if (feedbackIds.Count > 0) await _feedbackRepository.DeleteManyAsync(feedbackIds, cancellationToken: cancellationToken);
				await _messageRepository.DeleteManyAsync(messageIds, cancellationToken: cancellationToken);
			}

			await _conversationRepository.DeleteAsync(conversation, cancellationToken: cancellationToken);
		}

		private async Task<Conversation> GetOwnedConversationAsync(Guid id, CancellationToken cancellationToken)
		{
			var conversation = await _conversationRepository.GetAsync(id, cancellationToken: cancellationToken);
			if (conversation.CreatorId != CurrentUser.Id)
			{
				throw new AbpAuthorizationException("无权访问该会话");
			}

			return conversation;
		}

		private static ConversationDto MapToDto(Conversation conversation)
		{
			var scopeIds = ParseScopeIds(conversation.KnowledgeBaseScopeJson);

			// P5 之前的会话没有 scope JSON，回落到单库字段
			if (scopeIds.Count == 0 && conversation.KnowledgeBaseId is { } singleId) scopeIds.Add(singleId);

			return new ConversationDto
			{
				Id = conversation.Id,
				Title = conversation.Title,
				KnowledgeBaseId = conversation.KnowledgeBaseId,
				KnowledgeBaseIds = scopeIds,
				AllKnowledgeBases = conversation.ScopeAll,
				CreationTime = conversation.CreationTime,
				LastMessageAt = conversation.LastMessageAt
			};
		}

		/// <summary>
		/// 反序列化会话的提问范围；旧数据或非法 JSON 按空列表处理
		/// </summary>
		/// <param name="json">范围 JSON</param>
		/// <returns>知识库 ID 列表</returns>
		private static List<Guid> ParseScopeIds(string? json)
		{
			if (string.IsNullOrWhiteSpace(json)) return [];

			try
			{
				return JsonSerializer.Deserialize<List<Guid>>(json) ?? [];
			}
			catch (JsonException)
			{
				return [];
			}
		}

		/// <summary>
		/// 反序列化引用 JSON；旧数据格式不合法时按空引用返回，不能打断消息回放
		/// </summary>
		/// <param name="json">引用 JSON</param>
		/// <returns>引用列表</returns>
		private static List<RagCitationDto> ParseCitations(string? json)
		{
			if (string.IsNullOrWhiteSpace(json)) return [];

			try
			{
				return JsonSerializer.Deserialize<List<RagCitationDto>>(json) ?? [];
			}
			catch (JsonException)
			{
				return [];
			}
		}
	}
}
