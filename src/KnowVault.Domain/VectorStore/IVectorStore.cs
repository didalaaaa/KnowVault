namespace KnowVault.Domain
{
	/// <summary>向量库访问抽象（实现放 Infrastructure 层，见 TECH_STACK §9.1）。</summary>
	public interface IVectorStore
	{
		/// <summary>幂等创建 collection（已存在则什么都不做）。</summary>
		/// <param name="cancellationToken">取消令牌。</param>
		Task EnsureCollectionAsync(CancellationToken cancellationToken = default);

		/// <summary>批量写入/覆盖向量点。</summary>
		/// <param name="points">向量点集合。</param>
		/// <param name="cancellationToken">取消令牌。</param>
		Task UpsertAsync(IReadOnlyList<VectorPoint> points, CancellationToken cancellationToken = default);

		/// <summary>按过滤条件做语义检索。</summary>
		/// <param name="queryVector">查询向量。</param>
		/// <param name="filter">payload 过滤条件。</param>
		/// <param name="topK">返回条数。</param>
		/// <param name="cancellationToken">取消令牌。</param>
		/// <returns>按相似度降序的命中列表。</returns>
		Task<IReadOnlyList<VectorSearchHit>> SearchAsync(
			float[] queryVector,
			VectorFilter filter,
			int topK,
			CancellationToken cancellationToken = default);

		/// <summary>
		/// 按文档删除向量点（payload 过滤 tenantId + knowledgeBaseId + documentId）
		/// </summary>
		/// <param name="tenantId">租户 ID（单租户为 <see cref="Guid.Empty"/></param>
		/// <param name="knowledgeBaseId">知识库 ID</param>
		/// <param name="documentId">文档 ID</param>
		/// <param name="cancellationToken">取消令牌</param>
		/// <returns>任务</returns>
		Task DeleteByDocumentAsync(
			Guid tenantId,
			Guid knowledgeBaseId,
			Guid documentId,
			CancellationToken cancellationToken = default);
	}
}
