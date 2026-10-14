namespace KnowVault.Ai
{
	/// <summary>
	/// 对话式文本生成：只提供流式，一次性回答由 <see cref="RagService"/> 拼流得到，避免两套实现行为漂移
	/// </summary>
	public interface ILlmChatService
	{
		/// <summary>
		/// 以流式方式补全回答，逐段返回增量文本
		/// </summary>
		/// <param name="systemPrompt">系统提示词</param>
		/// <param name="messages">完整对话消息（含历史与最后一条用户消息）</param>
		/// <param name="cancellationToken">取消令牌</param>
		/// <returns>增量文本流</returns>
		IAsyncEnumerable<string> CompleteStreamAsync(string systemPrompt, IReadOnlyList<LlmChatMessage> messages, CancellationToken cancellationToken = default);
	}
}
