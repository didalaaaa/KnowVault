namespace KnowVault.Ai
{
	/// <summary>检索命中的切片（已从 Qdrant payload 解析出字段）。</summary>
	public class RagRetrievedChunk
	{
		/// <summary>文档 ID。</summary>
		public Guid DocumentId { get; set; }

		/// <summary>文档标题。</summary>
		public string DocumentTitle { get; set; } = string.Empty;

		/// <summary>切片序号。</summary>
		public int ChunkIndex { get; set; }

		/// <summary>标题层级路径。</summary>
		public string HeadingPath { get; set; } = string.Empty;

		/// <summary>切片正文。</summary>
		public string Content { get; set; } = string.Empty;

		/// <summary>相似度得分。</summary>
		public double Score { get; set; }
	}
}
