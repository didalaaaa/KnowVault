using System.ComponentModel.DataAnnotations;
using KnowVault.Domain.Shared;

namespace KnowVault.Application.Contracts
{

	/// <summary>创建文档输入（P0 仅记录元数据，文件对象存储与解析在 P1）。</summary>
	public class CreateDocumentInput
	{
		[Required]
		public Guid KnowledgeBaseId { get; set; }

		[Required]
		[StringLength(256)]
		public string Title { get; set; } = string.Empty;

		public DocumentSourceTypeEnum SourceType { get; set; }
	}

}
