using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowVault.Domain
{

	/// <summary>
	/// 知识库（多租户起步，结构预留 TenantId 隔离）。
	/// </summary>
	public class KnowledgeBase : CreationAuditedAggregateRoot<Guid>, IMultiTenant
	{
		public string Name { get; set; } = default!;

		public string? Description { get; set; }

		/// <summary>多租户隔离 ID；ABP 多租户是一等公民，先单租户运行。</summary>
		public Guid? TenantId { get; set; }

		protected KnowledgeBase()
		{
		}

		public KnowledgeBase(
			Guid id,
			string name,
			string? description = null,
			Guid? tenantId = null) : base(id)
		{
			Name = name;
			Description = description;
			TenantId = tenantId;
		}

		public void Update(string name, string? description = null)
		{
			Name = name;
			Description = description;
		}
	}

}
