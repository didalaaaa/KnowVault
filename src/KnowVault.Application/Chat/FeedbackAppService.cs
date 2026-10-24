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
	/// 消息反馈应用服务：只允许评价自己会话里的助手消息
	/// </summary>
	[Authorize]
	public class FeedbackAppService : KnowVaultAppService, IFeedbackAppService
	{
		private readonly IRepository<Message, Guid> _messageRepository;
		private readonly IRepository<Conversation, Guid> _conversationRepository;
		private readonly IRepository<MessageFeedback, Guid> _feedbackRepository;

		/// <summary>
		/// 构造函数
		/// </summary>
		/// <param name="messageRepository">消息仓储</param>
		/// <param name="conversationRepository">会话仓储</param>
		/// <param name="feedbackRepository">反馈仓储</param>
		public FeedbackAppService(
			IRepository<Message, Guid> messageRepository,
			IRepository<Conversation, Guid> conversationRepository,
			IRepository<MessageFeedback, Guid> feedbackRepository)
		{
			_messageRepository = messageRepository;
			_conversationRepository = conversationRepository;
			_feedbackRepository = feedbackRepository;
		}

		/// <inheritdoc />
		public async Task<FeedbackRatingEnum?> SetAsync(SetFeedbackInput input, CancellationToken cancellationToken)
		{
			var userId = CurrentUser.Id ?? throw new AbpAuthorizationException("请先登录");

			var message = await _messageRepository.GetAsync(input.MessageId, cancellationToken: cancellationToken);
			if (message.Role != MessageRoleEnum.Assistant) throw new UserFriendlyException("只能评价助手回答");

			var conversation = await _conversationRepository.GetAsync(message.ConversationId, cancellationToken: cancellationToken);
			if (conversation.CreatorId != userId) throw new AbpAuthorizationException("无权评价该回答");

			var existing = await _feedbackRepository.FindAsync(
				x => x.MessageId == input.MessageId && x.UserId == userId,
				cancellationToken: cancellationToken);

			if (input.Rating == null)
			{
				if (existing != null) await _feedbackRepository.DeleteAsync(existing, cancellationToken: cancellationToken);
				return null;
			}

			if (existing == null)
			{
				await _feedbackRepository.InsertAsync(
					new MessageFeedback(GuidGenerator.Create(), input.MessageId, userId, input.Rating.Value, input.Comment, CurrentTenant.Id),
					autoSave: true,
					cancellationToken: cancellationToken);
			}
			else
			{
				existing.Rating = input.Rating.Value;
				existing.Comment = input.Comment;
				await _feedbackRepository.UpdateAsync(existing, autoSave: true, cancellationToken: cancellationToken);
			}

			return input.Rating;
		}
	}
}
