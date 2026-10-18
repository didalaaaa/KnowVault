using KnowVault.Domain.Shared;

namespace KnowVault.Application.Contracts
{
	/// <summary>
	/// 知识库成员视图
	/// </summary>
	public class KnowledgeBaseMemberDto
	{
		/// <summary>
		/// 用户 ID
		/// </summary>
		public Guid UserId { get; set; }

		/// <summary>
		/// 用户名
		/// </summary>
		public string UserName { get; set; } = string.Empty;

		/// <summary>
		/// 库内角色
		/// </summary>
		public KnowledgeBaseRoleEnum Role { get; set; }
	}
}
