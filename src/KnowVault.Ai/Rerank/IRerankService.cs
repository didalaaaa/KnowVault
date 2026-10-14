namespace KnowVault.Ai
{
	/// <summary>
	/// 文本重排（Rerank）契约：把向量召回的候选按与问题的相关性重新排序
	/// </summary>
	/// <remarks>
	/// 接口留在 <c>KnowVault.Ai</c> 而不下沉 Domain，因为只有 Ai 层使用；
	/// 与 <c>IEmbeddingService</c> 不同（Ingest 需要它，故接口在 Domain）
	/// </remarks>
	public interface IRerankService
	{
		/// <summary>
		/// 对候选文档按与查询的相关性重排
		/// </summary>
		/// <param name="query">用户问题</param>
		/// <param name="documents">候选文档正文，顺序即返回结果中 <see cref="RerankResult.Index"/> 的语义</param>
		/// <param name="topN">返回条数上限</param>
		/// <param name="cancellationToken">取消令牌</param>
		/// <returns>按相关性降序的重排结果</returns>
		Task<IReadOnlyList<RerankResult>> RerankAsync(string query, IReadOnlyList<string> documents, int topN, CancellationToken cancellationToken = default);
	}
}
