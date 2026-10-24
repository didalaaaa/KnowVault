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

		/// <summary>
		/// 构造函数
		/// </summary>
		/// <param name="conversationRepository">会话仓储</param>
		/// <param name="messageRepository">消息仓储</param>
		public ConversationAppService(
			IRepository<Conversation, Guid> conversationRepository,
			IRepository<Message, Guid> messageRepository)
		{
			_conversationRepository = conversationRepository;
			_messageRepository = messageRepository;
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

			return [.. messages
				.OrderBy(x => x.CreationTime)
				.Select(x => new ChatMessageDto
				{
					Id = x.Id,
					Role = x.Role,
					Content = x.Content,
					Citations = ParseCitations(x.CitationsJson),
					CreationTime = x.CreationTime
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

			if (messageIds.Count > 0) await _messageRepository.DeleteManyAsync(messageIds, cancellationToken: cancellationToken);
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

		private static ConversationDto MapToDto(Conversation conversation) => new()
		{
			Id = conversation.Id,
			Title = conversation.Title,
			KnowledgeBaseId = conversation.KnowledgeBaseId,
			CreationTime = conversation.CreationTime,
			LastMessageAt = conversation.LastMessageAt
		};

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
