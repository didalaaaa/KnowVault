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

		public Guid? TenantId { get; set; }

		protected Conversation()
		{
		}

		public Conversation(
			Guid id,
			string title,
			Guid? tenantId = null) : base(id)
		{
			Title = title;
			TenantId = tenantId;
		}

		public void Rename(string title) => Title = title;
	}

}
