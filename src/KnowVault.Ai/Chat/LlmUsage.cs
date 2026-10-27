namespace KnowVault.Ai
{
	/// <summary>
	/// 一次模型调用的 token 用量
	/// </summary>
	public class LlmUsage
	{
		/// <summary>
		/// 输入 token 数
		/// </summary>
		public int PromptTokens { get; init; }

		/// <summary>
		/// 输出 token 数
		/// </summary>
		public int CompletionTokens { get; init; }

		/// <summary>
		/// 是否由本地估算（模型没有返回 usage）
		/// </summary>
		public bool IsEstimated { get; init; }
	}
}
