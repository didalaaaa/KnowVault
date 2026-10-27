namespace KnowVault.Domain
{
	/// <summary>
	/// AI 模型计价配置（人民币 / 百万 token）
	/// </summary>
	public class AiPricingOptions
	{
		/// <summary>
		/// 计价币种
		/// </summary>
		public string Currency { get; set; } = "CNY";

		/// <summary>
		/// DeepSeek 输入单价（元 / 百万 token）
		/// </summary>
		public decimal DeepSeekInputPerMillion { get; set; } = 2m;

		/// <summary>
		/// DeepSeek 输出单价（元 / 百万 token）
		/// </summary>
		public decimal DeepSeekOutputPerMillion { get; set; } = 8m;

		/// <summary>
		/// Embedding 单价（元 / 百万 token）
		/// </summary>
		public decimal EmbeddingPerMillion { get; set; } = 0.07m;

		/// <summary>
		/// Rerank 单价（元 / 百万 token）
		/// </summary>
		public decimal RerankPerMillion { get; set; } = 0.07m;
	}
}
