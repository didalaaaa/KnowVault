namespace KnowVault.Ai
{
	/// <summary>
	/// 对话式文本生成：流式用于问答，非流式用于改写等短调用
	/// </summary>
	public interface ILlmChatService
	{
		/// <summary>
		/// 以流式方式补全回答，逐段返回增量文本与可选的 usage
		/// </summary>
		/// <param name="systemPrompt">系统提示词</param>
		/// <param name="messages">完整对话消息（含历史与最后一条用户消息）</param>
		/// <param name="cancellationToken">取消令牌</param>
		/// <returns>增量文本流；最后一帧可能只带 usage</returns>
		IAsyncEnumerable<LlmStreamChunk> CompleteStreamAsync(string systemPrompt, IReadOnlyList<LlmChatMessage> messages, CancellationToken cancellationToken = default);

		/// <summary>
		/// 非流式补全，用于查询改写等短调用
		/// </summary>
		/// <param name="systemPrompt">系统提示词</param>
		/// <param name="messages">完整对话消息</param>
		/// <param name="maxTokens">最大输出 token 数</param>
		/// <param name="temperature">采样温度</param>
		/// <param name="cancellationToken">取消令牌</param>
		/// <returns>完整文本、模型名与可选的 usage</returns>
		Task<LlmCompletionResult> CompleteAsync(
			string systemPrompt,
			IReadOnlyList<LlmChatMessage> messages,
			int maxTokens,
			double temperature,
			CancellationToken cancellationToken = default);
	}
}
