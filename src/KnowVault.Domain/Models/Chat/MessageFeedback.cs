using KnowVault.Domain.Shared;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowVault.Domain
{
	/// <summary>
	/// 助手消息反馈：同一用户对同一消息只保留一条
	/// </summary>
	public class MessageFeedback : CreationAuditedAggregateRoot<Guid>, IMultiTenant
	{
		/// <summary>
		/// 被评价的消息 ID
		/// </summary>
		public Guid MessageId { get; set; }

		/// <summary>
		/// 评价人用户 ID
		/// </summary>
		public Guid UserId { get; set; }

		/// <summary>
		/// 评价类型
		/// </summary>
		public FeedbackRatingEnum Rating { get; set; }

		/// <summary>
		/// 可选备注
		/// </summary>
		public string? Comment { get; set; }

		/// <summary>
		/// 租户 ID，单租户运行时为空
		/// </summary>
		public Guid? TenantId { get; set; }

		protected MessageFeedback()
		{
		}

		/// <summary>
		/// 构造函数
		/// </summary>
		/// <param name="id">反馈 ID</param>
		/// <param name="messageId">消息 ID</param>
		/// <param name="userId">评价人用户 ID</param>
		/// <param name="rating">评价类型</param>
		/// <param name="comment">可选备注</param>
		/// <param name="tenantId">租户 ID</param>
		public MessageFeedback(
			Guid id,
			Guid messageId,
			Guid userId,
			FeedbackRatingEnum rating,
			string? comment = null,
			Guid? tenantId = null) : base(id)
		{
			MessageId = messageId;
			UserId = userId;
			Rating = rating;
			Comment = comment;
			TenantId = tenantId;
		}
	}
}
