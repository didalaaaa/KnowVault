namespace KnowVault.Ingest
{
	/// <summary>文档处理入口：解析 → 切片 → 向量化 → 写入 Qdrant → 推进状态。</summary>
	public interface IDocumentProcessor
	{
		/// <summary>处理单个文档（失败时把错误写进文档状态，不抛异常）。</summary>
		/// <param name="documentId">文档 ID。</param>
		/// <param name="cancellationToken">取消令牌。</param>
		Task ProcessAsync(Guid documentId, CancellationToken cancellationToken = default);
	}
}
