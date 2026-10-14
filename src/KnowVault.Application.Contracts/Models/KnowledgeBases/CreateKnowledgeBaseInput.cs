using System.ComponentModel.DataAnnotations;

namespace KnowVault.Application.Contracts
{
	/// <summary>新建知识库入参。</summary>
	public class CreateKnowledgeBaseInput
	{
		/// <summary>名称（首尾空白由服务端裁剪）。</summary>
		[Required]
		[StringLength(128)]
		public string Name { get; set; } = string.Empty;

		/// <summary>描述（可选）。</summary>
		[StringLength(512)]
		public string? Description { get; set; }
	}
}
