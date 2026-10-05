namespace KnowVault.Ai
{
	/// <summary>对话式文本生成（P1 只需一次性补全；流式留 P2）。</summary>
	public interface ILlmChatService
	{
		/// <summary>按 system + user 组装并生成回答。</summary>
		/// <param name="systemPrompt">系统提示词（强约束回答范围）。</param>
		/// <param name="userPrompt">用户提示词（含检索片段与问题）。</param>
		/// <param name="cancellationToken">取消令牌。</param>
		/// <returns>模型返回的纯文本回答。</returns>
		Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default);
	}
}
