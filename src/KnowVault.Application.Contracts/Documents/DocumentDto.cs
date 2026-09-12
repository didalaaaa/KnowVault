using System;
using KnowVault.Domain.Shared.Documents;

namespace KnowVault.Application.Contracts.Documents
{

	/// <summary>文档 DTO。</summary>
	public class DocumentDto
	{
	    public Guid Id { get; set; }
	    public Guid KnowledgeBaseId { get; set; }
	    public string Title { get; set; } = string.Empty;
	    public DocumentSourceType SourceType { get; set; }
	    public DocumentStatus Status { get; set; }
	    public string? ErrorMessage { get; set; }
	    public DateTime CreationTime { get; set; }
	}

}
