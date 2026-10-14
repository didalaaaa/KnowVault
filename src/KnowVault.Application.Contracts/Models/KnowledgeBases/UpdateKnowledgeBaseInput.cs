using System.ComponentModel.DataAnnotations;

namespace KnowVault.Application.Contracts
{
	/// <summary>修改知识库入参。</summary>
	public class UpdateKnowledgeBaseInput
	{
		/// <summary>知识库 ID（ABP 约定：入参带 Id 时映射为 PUT 路由的 {id}）。</summary>
		public Guid Id { get; set; }

		/// <summary>名称（首尾空白由服务端裁剪）。</summary>
		[Required]
		[StringLength(128)]
		public string Name { get; set; } = string.Empty;

		/// <summary>描述（可选）。</summary>
		[StringLength(512)]
		public string? Description { get; set; }
	}
}
