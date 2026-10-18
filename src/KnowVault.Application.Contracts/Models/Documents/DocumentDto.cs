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

		/// <summary>已尝试摄入次数。</summary>
		public int IngestAttemptCount { get; set; }

		/// <summary>对象存储（MinIO）中的对象键；上传成功后才会有值。</summary>
		public string? ObjectStorageKey { get; set; }

		public DateTime CreationTime { get; set; }
	}

}
