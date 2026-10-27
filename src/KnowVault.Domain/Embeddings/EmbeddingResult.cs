namespace KnowVault.Domain
{
	/// <summary>
	/// 一批文本的向量化结果与用量
	/// </summary>
	public class EmbeddingResult
	{
		/// <summary>
		/// 向量列表，顺序与输入严格一致
		/// </summary>
		public required IReadOnlyList<float[]> Vectors { get; init; }

		/// <summary>
		/// 实际使用的模型名
		/// </summary>
		public required string Model { get; init; }

		/// <summary>
		/// 输入 token 总数
		/// </summary>
		public int TotalTokens { get; init; }

		/// <summary>
		/// 是否由本地估算 token
		/// </summary>
		public bool IsEstimated { get; init; }
	}
}
