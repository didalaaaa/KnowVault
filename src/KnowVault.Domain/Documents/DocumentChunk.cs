using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowVault.Domain
{

	/// <summary>
	/// 切片的元数据。Id 与 Qdrant 的 pointId 对齐；
	/// 切片正文与溯源信息存 Qdrant payload，这里只冗余一份文档级元数据。
	/// </summary>
	public class DocumentChunk : CreationAuditedAggregateRoot<Guid>, IMultiTenant
	{
		public Guid DocumentId { get; set; }

		/// <summary>切片序号（0 起）。</summary>
		public int ChunkIndex { get; set; }

		/// <summary>切片正文（为便于调试/溯源，PostgreSQL 也冗余一份）。</summary>
		public string Content { get; set; } = default!;

		public Guid? TenantId { get; set; }

		protected DocumentChunk()
		{
		}

		public DocumentChunk(
			Guid id,
			Guid documentId,
			int chunkIndex,
			string content,
			Guid? tenantId = null) : base(id)
		{
			DocumentId = documentId;
			ChunkIndex = chunkIndex;
			Content = content;
			TenantId = tenantId;
		}
	}

}
