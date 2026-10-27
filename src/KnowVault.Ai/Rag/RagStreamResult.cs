namespace KnowVault.Ai
{
	/// <summary>
	/// 流式问答的第一步结果：先给出引用切片，再给出增量文本流
	/// </summary>
	public class RagStreamResult
	{
		/// <summary>
		/// 检索（并按需重排）后的引用切片，顺序即引用编号顺序
		/// </summary>
		public required IReadOnlyList<RagRetrievedChunk> Chunks { get; init; }

		/// <summary>
		/// 实际参与召回的改写变体（不含原问题）
		/// </summary>
		public required IReadOnlyList<string> RewriteQueries { get; init; }

		/// <summary>
		/// 大模型生成的增量文本流；最后一帧可能只带 usage
		/// </summary>
		public required IAsyncEnumerable<LlmStreamChunk> Deltas { get; init; }
	}
}
