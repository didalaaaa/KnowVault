namespace KnowVault.Ai
{
	/// <summary>检索命中的切片（已从 Qdrant payload 解析出字段）。</summary>
	public class RagRetrievedChunk
	{
		/// <summary>文档 ID。</summary>
		public Guid DocumentId { get; set; }

		/// <summary>文档所属知识库 ID。</summary>
		public Guid KnowledgeBaseId { get; set; }

		/// <summary>文档标题。</summary>
		public string DocumentTitle { get; set; } = string.Empty;

		/// <summary>切片序号。</summary>
		public int ChunkIndex { get; set; }

		/// <summary>标题层级路径。</summary>
		public string HeadingPath { get; set; } = string.Empty;

		/// <summary>切片正文。</summary>
		public string Content { get; set; } = string.Empty;

		/// <summary>相似度得分（重排接入后为相关性得分，见 TECH_STACK §9.2）。</summary>
		public double Score { get; set; }

		/// <summary>返回替换了得分的新实例（重排分与向量分语义不同，不原地混用）。</summary>
		/// <param name="score">新的得分。</param>
		/// <returns>新的切片对象。</returns>
		public RagRetrievedChunk WithScore(double score) => new()
		{
			DocumentId = DocumentId,
			KnowledgeBaseId = KnowledgeBaseId,
			DocumentTitle = DocumentTitle,
			ChunkIndex = ChunkIndex,
			HeadingPath = HeadingPath,
			Content = Content,
			Score = score
		};
	}
}
