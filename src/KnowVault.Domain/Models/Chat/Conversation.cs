using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowVault.Domain
{

	/// <summary>
	/// 会话：从第一天就持久化，为二期多轮对话 / 历史打底。
	/// </summary>
	public class Conversation : CreationAuditedAggregateRoot<Guid>, IMultiTenant
	{
		public string Title { get; set; } = default!;

		/// <summary>
		/// 会话归属的知识库；历史数据为空
		/// </summary>
		public Guid? KnowledgeBaseId { get; set; }

		/// <summary>
		/// 最后一条消息时间，用于会话列表排序；为空时回落创建时间
		/// </summary>
		public DateTime? LastMessageAt { get; set; }

		public Guid? TenantId { get; set; }

		protected Conversation()
		{
		}

		public Conversation(
			Guid id,
			string title,
			Guid? tenantId = null,
			Guid? knowledgeBaseId = null) : base(id)
		{
			Title = title;
			TenantId = tenantId;
			KnowledgeBaseId = knowledgeBaseId;
		}

		/// <summary>
		/// 重命名会话
		/// </summary>
		/// <param name="title">新标题</param>
		public void Rename(string title) => Title = title;

		/// <summary>
		/// 记录一次会话活动
		/// </summary>
		/// <param name="at">活动时间</param>
		public void Touch(DateTime at) => LastMessageAt = at;
	}

}
