namespace KnowVault.Ai
{
	/// <summary>
	/// 非流式补全的结果
	/// </summary>
	public class LlmCompletionResult
	{
		/// <summary>
		/// 模型返回的完整文本
		/// </summary>
		public required string Content { get; init; }

		/// <summary>
		/// 实际使用的模型名
		/// </summary>
		public required string Model { get; init; }

		/// <summary>
		/// token 用量；模型或连接器未返回时为空
		/// </summary>
		public LlmUsage? Usage { get; init; }
	}
}
