namespace KnowVault.Ai
{

	/// <summary>
	/// RAG 编排入口：检索 -> 重排 -> 组装 Prompt -> 生成 -> 引用溯源
	/// </summary>
	public interface IRagService
	{
		/// <summary>
		/// 一次性问答（内部拼流得到整段回答）
		/// </summary>
		/// <param name="request">提问入参</param>
		/// <param name="cancellationToken">取消令牌</param>
		/// <returns>整段回答与引用</returns>
		Task<RagAnswer> AnswerAsync(RagRequest request, CancellationToken cancellationToken = default);

		/// <summary>
		/// 流式问答：先返回引用切片，再给出增量文本流
		/// </summary>
		/// <param name="request">提问入参</param>
		/// <param name="cancellationToken">取消令牌</param>
		/// <returns>引用切片与增量文本流</returns>
		Task<RagStreamResult> AnswerStreamAsync(RagRequest request, CancellationToken cancellationToken = default);

		/// <summary>
		/// 只做检索与重排，不调用生成模型（评估工具与单测使用）
		/// </summary>
		/// <param name="request">提问入参</param>
		/// <param name="cancellationToken">取消令牌</param>
		/// <returns>引用切片与实际参与召回的查询</returns>
		Task<RagRetrievalResult> RetrieveAsync(RagRequest request, CancellationToken cancellationToken = default);
	}

}
