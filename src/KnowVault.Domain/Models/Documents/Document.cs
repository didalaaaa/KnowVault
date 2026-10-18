using KnowVault.Domain.Shared;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowVault.Domain
{

	/// <summary>
	/// 文档：原件存对象存储（MinIO），元数据存 PostgreSQL。
	/// </summary>
	public class Document : CreationAuditedAggregateRoot<Guid>, IMultiTenant
	{
		public Guid KnowledgeBaseId { get; set; }

		public string Title { get; set; } = default!;

		public DocumentSourceTypeEnum SourceType { get; set; }

		/// <summary>MinIO 中的对象存储 Key / object name。</summary>
		public string? ObjectStorageKey { get; set; }

		public DocumentStatusEnum Status { get; set; } = DocumentStatusEnum.Pending;

		public string? ErrorMessage { get; set; }

		/// <summary>
		/// 已尝试摄入的次数（含首次）；失败重试时累加，便于排查
		/// </summary>
		public int IngestAttemptCount { get; private set; }

		public Guid? TenantId { get; set; }

		protected Document()
		{
		}

		public Document(
			Guid id,
			Guid knowledgeBaseId,
			string title,
			DocumentSourceTypeEnum sourceType,
			Guid? tenantId = null) : base(id)
		{
			KnowledgeBaseId = knowledgeBaseId;
			Title = title;
			SourceType = sourceType;
			TenantId = tenantId;
		}

		public void MarkProcessing()
		{
			Status = DocumentStatusEnum.Processing;
			ErrorMessage = null;
		}

		public void MarkReady() => Status = DocumentStatusEnum.Ready;

		public void MarkFailed(string error)
		{
			Status = DocumentStatusEnum.Failed;
			ErrorMessage = error;
		}

		/// <summary>
		/// 记录一次摄入尝试
		/// </summary>
		public void RecordAttempt() => IngestAttemptCount++;

		/// <summary>
		/// 重新排队：清掉上次失败原因，等待消费
		/// </summary>
		public void MarkPending()
		{
			Status = DocumentStatusEnum.Pending;
			ErrorMessage = null;
		}
	}

}
