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

		/// <summary>标题层级路径，历史数据为空</summary>
		public string? HeadingPath { get; set; }

		public Guid? TenantId { get; set; }

		protected DocumentChunk()
		{
		}

		/// <summary>
		/// 构造函数
		/// </summary>
		/// <param name="id">切片 ID（与 Qdrant pointId 一致）</param>
		/// <param name="documentId">所属文档</param>
		/// <param name="chunkIndex">切片序号</param>
		/// <param name="content">切片正文</param>
		/// <param name="headingPath">标题层级路径</param>
		/// <param name="tenantId">租户 ID，单租户运行时为空</param>
		public DocumentChunk(
			Guid id,
			Guid documentId,
			int chunkIndex,
			string content,
			string? headingPath = null,
			Guid? tenantId = null) : base(id)
		{
			DocumentId = documentId;
			ChunkIndex = chunkIndex;
			Content = content;
			HeadingPath = headingPath;
			TenantId = tenantId;
		}
	}

}
