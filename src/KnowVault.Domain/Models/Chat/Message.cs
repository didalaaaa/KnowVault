using KnowVault.Domain.Shared;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowVault.Domain
{

	/// <summary>
	/// 消息：含引用溯源（CitationsJson），供前端展示引用出处。
	/// </summary>
	public class Message : CreationAuditedAggregateRoot<Guid>, IMultiTenant
	{
		public Guid ConversationId { get; set; }

		public MessageRoleEnum Role { get; set; }

		public string Content { get; set; } = default!;

		/// <summary>
		/// 引用溯源（JSON 数组：documentId/title/chunkIndex/score），可空
		/// </summary>
		public string? CitationsJson { get; set; }

		/// <summary>
		/// 助手回答耗时（毫秒），仅助手消息有值
		/// </summary>
		public int? LatencyMs { get; private set; }

		public Guid? TenantId { get; set; }

		protected Message()
		{
		}

		/// <summary>
		/// 记录生成耗时
		/// </summary>
		/// <param name="milliseconds">耗时毫秒数</param>
		public void SetLatency(int milliseconds) => LatencyMs = milliseconds;

		public Message(
			Guid id,
			Guid conversationId,
			MessageRoleEnum role,
			string content,
			string? citationsJson = null,
			Guid? tenantId = null) : base(id)
		{
			ConversationId = conversationId;
			Role = role;
			Content = content;
			CitationsJson = citationsJson;
			TenantId = tenantId;
		}
	}

}
