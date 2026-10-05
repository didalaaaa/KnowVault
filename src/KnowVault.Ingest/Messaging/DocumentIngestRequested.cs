namespace KnowVault.Ingest
{
	/// <summary>「文档待摄入」消息。用可写属性（而非 record 位置参数），保证跨进程序列化稳定。</summary>
	public class DocumentIngestRequested
	{
		/// <summary>文档 ID。</summary>
		public Guid DocumentId { get; init; }

		/// <summary>租户 ID。</summary>
		public Guid? TenantId { get; init; }
	}
}
