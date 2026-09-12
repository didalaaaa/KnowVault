using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;
using KnowVault.Domain.Shared.Documents;

namespace KnowVault.Domain.Documents
{

	/// <summary>
	/// 文档：原件存对象存储（MinIO），元数据存 PostgreSQL。
	/// </summary>
	public class Document : CreationAuditedAggregateRoot<Guid>, IMultiTenant
	{
	    public Guid KnowledgeBaseId { get; set; }

	    public string Title { get; set; } = default!;

	    public DocumentSourceType SourceType { get; set; }

	    /// <summary>MinIO 中的对象存储 Key / object name。</summary>
	    public string? ObjectStorageKey { get; set; }

	    public DocumentStatus Status { get; set; } = DocumentStatus.Pending;

	    public string? ErrorMessage { get; set; }

	    public Guid? TenantId { get; set; }

	    protected Document()
	    {
	    }

	    public Document(
	        Guid id,
	        Guid knowledgeBaseId,
	        string title,
	        DocumentSourceType sourceType,
	        Guid? tenantId = null) : base(id)
	    {
	        KnowledgeBaseId = knowledgeBaseId;
	        Title = title;
	        SourceType = sourceType;
	        TenantId = tenantId;
	    }

	    public void MarkProcessing() => Status = DocumentStatus.Processing;

	    public void MarkReady() => Status = DocumentStatus.Ready;

	    public void MarkFailed(string error)
	    {
	        Status = DocumentStatus.Failed;
	        ErrorMessage = error;
	    }
	}

}
