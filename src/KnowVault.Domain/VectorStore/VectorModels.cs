namespace KnowVault.Domain
{
	/// <summary>待写入的向量点。</summary>
	public class VectorPoint
	{
		/// <summary>点 ID，取 <c>DocumentChunk.Id</c>。</summary>
		public Guid Id { get; set; }

		/// <summary>向量数据。</summary>
		public float[] Vector { get; set; } = [];

		/// <summary>
		/// 关键词稀疏向量；为空表示该切片没有可索引词元
		/// </summary>
		public SparseVectorData? SparseVector { get; set; }

		/// <summary>payload（字段名见 <see cref="ChunkPayload"/>）。</summary>
		public Dictionary<string, object> Payload { get; set; } = [];
	}

	/// <summary>检索过滤条件（租户 + 一个或多个知识库）。</summary>
	public class VectorFilter
	{
		/// <summary>租户 ID；调用方须把 null 归一化为 <see cref="Guid.Empty"/>。</summary>
		public Guid TenantId { get; set; }

		/// <summary>
		/// 知识库 ID 集合；空集合表示不查询
		/// </summary>
		public IReadOnlyList<Guid> KnowledgeBaseIds { get; set; } = [];
	}

	/// <summary>检索命中。</summary>
	public class VectorSearchHit
	{
		/// <summary>点 ID（即 chunk ID）。</summary>
		public Guid Id { get; set; }

		/// <summary>相似度得分。</summary>
		public double Score { get; set; }

		/// <summary>payload。</summary>
		public Dictionary<string, object> Payload { get; set; } = [];
	}
}
