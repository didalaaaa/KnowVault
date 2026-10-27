using KnowVault.Domain;
using KnowVault.Domain.Shared;
using Xunit;

namespace KnowVault.Tests
{
	/// <summary>
	/// AI 费用换算的纯函数测试
	/// </summary>
	public class AiUsageCostCalculatorTests
	{
		/// <summary>
		/// 生成类调用按输入、输出分别计价后相加
		/// </summary>
		[Fact]
		public void Chat_prices_input_and_output_separately()
		{
			var cost = AiUsageCostCalculator.Calculate(AiOperationEnum.Chat, 500_000, 250_000, new AiPricingOptions());

			Assert.Equal(3m, cost);
		}

		/// <summary>
		/// 查询改写与生成共用 DeepSeek 单价
		/// </summary>
		[Fact]
		public void QueryRewrite_uses_deepseek_pricing()
		{
			var cost = AiUsageCostCalculator.Calculate(AiOperationEnum.QueryRewrite, 1_000_000, 0, new AiPricingOptions());

			Assert.Equal(2m, cost);
		}

		/// <summary>
		/// 向量化与重排按总 token 数乘以各自单价
		/// </summary>
		[Fact]
		public void Embedding_and_rerank_use_total_tokens()
		{
			var pricing = new AiPricingOptions { EmbeddingPerMillion = 0.07m, RerankPerMillion = 0.14m };

			Assert.Equal(0.07m, AiUsageCostCalculator.Calculate(AiOperationEnum.Embedding, 1_000_000, 0, pricing));
			Assert.Equal(0.28m, AiUsageCostCalculator.Calculate(AiOperationEnum.Rerank, 1_000_000, 1_000_000, pricing));
		}

		/// <summary>
		/// 结果保留六位小数并按远离零方向进位
		/// </summary>
		[Fact]
		public void Cost_rounds_to_six_decimals_away_from_zero()
		{
			var pricing = new AiPricingOptions { EmbeddingPerMillion = 0.5m };

			Assert.Equal(0.000002m, AiUsageCostCalculator.Calculate(AiOperationEnum.Embedding, 3, 0, pricing));
		}

		/// <summary>
		/// 单价全为零时费用为零
		/// </summary>
		[Fact]
		public void Zero_pricing_produces_zero_cost()
		{
			var pricing = new AiPricingOptions
			{
				DeepSeekInputPerMillion = 0,
				DeepSeekOutputPerMillion = 0,
				EmbeddingPerMillion = 0,
				RerankPerMillion = 0
			};

			Assert.Equal(0m, AiUsageCostCalculator.Calculate(AiOperationEnum.Chat, 10_000, 5_000, pricing));
		}
	}
}
