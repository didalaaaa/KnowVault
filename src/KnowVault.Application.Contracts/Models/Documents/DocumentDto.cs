using KnowVault.Domain.Shared;

namespace KnowVault.Application.Contracts
{

	/// <summary>文档 DTO。</summary>
	public class DocumentDto
	{
		public Guid Id { get; set; }
		public Guid KnowledgeBaseId { get; set; }
		public string Title { get; set; } = string.Empty;
		public DocumentSourceTypeEnum SourceType { get; set; }
		public DocumentStatusEnum Status { get; set; }
		public string? ErrorMessage { get; set; }
		public DateTime CreationTime { get; set; }
	}

}
