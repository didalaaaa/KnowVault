namespace KnowVault.Ai
{
	/// <summary>
	/// 流式生成的一帧：通常带增量文本，最后一帧可能只带 usage
	/// </summary>
	public class LlmStreamChunk
	{
		/// <summary>
		/// 增量文本；usage 帧可能为空
		/// </summary>
		public string? Delta { get; init; }

		/// <summary>
		/// token 用量；只有模型返回时才非空
		/// </summary>
		public LlmUsage? Usage { get; init; }
	}
}
