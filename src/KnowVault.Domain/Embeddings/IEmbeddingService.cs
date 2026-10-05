namespace KnowVault.Domain
{
	/// <summary>
	/// 文本向量化（Embedding）契约。
	/// 接口放 Domain（Ingest 与 Ai 都要用），实现放 <c>KnowVault.Ai</c>（Qwen）。
	/// </summary>
	public interface IEmbeddingService
	{
		/// <summary>把一批文本转成向量，返回顺序与输入严格一致。</summary>
		/// <param name="texts">待向量化的文本（不得为空字符串）。</param>
		/// <param name="cancellationToken">取消令牌。</param>
		/// <returns>与 <paramref name="texts"/> 等长的向量数组。</returns>
		Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default);
	}
}
