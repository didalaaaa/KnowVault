using KnowVault.Domain.Shared;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowVault.Domain
{
	/// <summary>
	/// 知识库成员：把用户与知识库绑定，并记录其在库内的角色
	/// </summary>
	public class KnowledgeBaseMember : CreationAuditedAggregateRoot<Guid>, IMultiTenant
	{
		/// <summary>
		/// 所属知识库 ID
		/// </summary>
		public Guid KnowledgeBaseId { get; private set; }

		/// <summary>
		/// 成员用户 ID
		/// </summary>
		public Guid UserId { get; private set; }

		/// <summary>
		/// 库内角色
		/// </summary>
		public KnowledgeBaseRoleEnum Role { get; private set; }

		/// <summary>
		/// 多租户隔离 ID；单租户运行时为 null
		/// </summary>
		public Guid? TenantId { get; private set; }

		/// <summary>
		/// EF Core 物化用
		/// </summary>
		protected KnowledgeBaseMember()
		{
		}

		/// <summary>
		/// 创建成员关系
		/// </summary>
		/// <param name="id">主键</param>
		/// <param name="knowledgeBaseId">知识库 ID</param>
		/// <param name="userId">用户 ID</param>
		/// <param name="role">库内角色</param>
		/// <param name="tenantId">租户 ID</param>
		public KnowledgeBaseMember(
			Guid id,
			Guid knowledgeBaseId,
			Guid userId,
			KnowledgeBaseRoleEnum role,
			Guid? tenantId = null) : base(id)
		{
			KnowledgeBaseId = knowledgeBaseId;
			UserId = userId;
			Role = role;
			TenantId = tenantId;
		}

		/// <summary>
		/// 调整成员角色
		/// </summary>
		/// <param name="role">新角色</param>
		public void ChangeRole(KnowledgeBaseRoleEnum role) => Role = role;
	}
}
