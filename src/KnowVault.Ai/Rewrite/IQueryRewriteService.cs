namespace KnowVault.Ai
{
	/// <summary>
	/// 查询改写：把原问题扩写成若干条用于检索的替代表述
	/// </summary>
	public interface IQueryRewriteService
	{
		/// <summary>
		/// 生成检索用的替代表述（不含原问题）
		/// </summary>
		/// <param name="question">原问题</param>
		/// <param name="history">最近的历史消息（时间升序）</param>
		/// <param name="cancellationToken">取消令牌</param>
		/// <returns>变体查询、模型名与可选的 usage</returns>
		Task<QueryRewriteResult> RewriteAsync(
			string question,
			IReadOnlyList<LlmChatMessage> history,
			CancellationToken cancellationToken = default);
	}

	/// <summary>
	/// 查询改写结果
	/// </summary>
	public class QueryRewriteResult
	{
		/// <summary>
		/// 变体查询列表（不含原问题）
		/// </summary>
		public required IReadOnlyList<string> Queries { get; init; }

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
