using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace KnowVault.Application.Contracts
{
	/// <summary>
	/// 知识库设置入参（四个字段都可空，空表示回落全局配置）
	/// </summary>
	public class KnowledgeBaseSettingsInput : EntityDto<Guid>
	{
		/// <summary>切片长度（字符），为空回落全局配置。</summary>
		public int? ChunkSizeChars { get; set; }

		/// <summary>切片重叠（字符），为空回落全局配置。</summary>
		public int? ChunkOverlapChars { get; set; }

		/// <summary>检索条数，为空回落全局配置。</summary>
		public int? TopK { get; set; }

		/// <summary>系统提示词，为空使用内置模板。</summary>
		[StringLength(4000)]
		public string? SystemPrompt { get; set; }
	}
}
