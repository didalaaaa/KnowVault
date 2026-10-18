using System.ComponentModel.DataAnnotations;
using KnowVault.Domain.Shared;

namespace KnowVault.Application.Contracts
{
	/// <summary>
	/// 新增或调整知识库成员
	/// </summary>
	public class SetKnowledgeBaseMemberInput
	{
		/// <summary>
		/// 知识库 ID
		/// </summary>
		[Required]
		public Guid KnowledgeBaseId { get; set; }

		/// <summary>
		/// 目标用户 ID
		/// </summary>
		[Required]
		public Guid UserId { get; set; }

		/// <summary>
		/// 目标角色
		/// </summary>
		[Required]
		public KnowledgeBaseRoleEnum Role { get; set; }
	}
}
