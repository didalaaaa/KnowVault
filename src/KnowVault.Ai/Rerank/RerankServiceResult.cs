namespace KnowVault.Ai
{
	/// <summary>
	/// 重排结果与用量
	/// </summary>
	public class RerankServiceResult
	{
		/// <summary>
		/// 按相关性降序的重排结果
		/// </summary>
		public required IReadOnlyList<RerankResult> Results { get; init; }

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
