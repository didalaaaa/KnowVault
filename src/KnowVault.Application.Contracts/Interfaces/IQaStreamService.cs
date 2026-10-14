namespace KnowVault.Application.Contracts
{
	/// <summary>
	/// 流式问答编排：负责会话与消息落库，并把问答过程以事件流返回
	/// </summary>
	public interface IQaStreamService
	{
		/// <summary>
		/// 流式提问：先落用户消息并取出历史，再检索、生成、落助手消息
		/// </summary>
		/// <param name="input">提问参数</param>
		/// <param name="cancellationToken">取消令牌（SignalR 传 <c>Hub.Context.ConnectionAborted</c>）</param>
		/// <returns>问答事件流</returns>
		IAsyncEnumerable<QaStreamEventDto> AskStreamAsync(AskQuestionInput input, CancellationToken cancellationToken = default);
	}
}
