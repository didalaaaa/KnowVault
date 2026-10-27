using KnowVault.Domain.Shared;

namespace KnowVault.Domain
{
	/// <summary>
	/// 按配置单价把 token 用量换算成估算费用
	/// </summary>
	public static class AiUsageCostCalculator
	{
		private const decimal Million = 1_000_000m;

		/// <summary>
		/// 计算一次调用的估算费用
		/// </summary>
		/// <param name="operation">AI 调用类型</param>
		/// <param name="promptTokens">输入 token 数</param>
		/// <param name="completionTokens">输出 token 数</param>
		/// <param name="pricing">计价配置</param>
		/// <returns>保留六位小数的估算费用（人民币元）</returns>
		public static decimal Calculate(
			AiOperationEnum operation,
			int promptTokens,
			int completionTokens,
			AiPricingOptions pricing)
		{
			var cost = operation switch
			{
				AiOperationEnum.Chat or AiOperationEnum.QueryRewrite =>
					promptTokens / Million * pricing.DeepSeekInputPerMillion +
					completionTokens / Million * pricing.DeepSeekOutputPerMillion,
				AiOperationEnum.Embedding =>
					(promptTokens + completionTokens) / Million * pricing.EmbeddingPerMillion,
				AiOperationEnum.Rerank =>
					(promptTokens + completionTokens) / Million * pricing.RerankPerMillion,
				_ => 0m
			};

			return decimal.Round(cost, 6, MidpointRounding.AwayFromZero);
		}
	}
}
