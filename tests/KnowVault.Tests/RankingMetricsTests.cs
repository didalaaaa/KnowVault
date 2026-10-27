using KnowVault.Ai;
using Xunit;

namespace KnowVault.Tests
{
	/// <summary>
	/// 检索排序指标的纯函数测试：二值相关性下 Recall / MRR / nDCG 的已知算例
	/// </summary>
	public class RankingMetricsTests
	{
		private static readonly Guid A = Guid.Parse("00000000-0000-0000-0000-000000000001");
		private static readonly Guid B = Guid.Parse("00000000-0000-0000-0000-000000000002");
		private static readonly Guid C = Guid.Parse("00000000-0000-0000-0000-000000000003");
		private static readonly Guid X = Guid.Parse("00000000-0000-0000-0000-000000000099");

		/// <summary>
		/// 第一条即命中时三个指标都取满分
		/// </summary>
		[Fact]
		public void First_rank_hit_scores_one()
		{
			Assert.Equal(1.0, RankingMetrics.RecallAtK([A, X], [A], 5), 6);
			Assert.Equal(1.0, RankingMetrics.MrrAtK([A, X], [A], 5), 6);
			Assert.Equal(1.0, RankingMetrics.NdcgAtK([A, X], [A], 5), 6);
		}

		/// <summary>
		/// 第三条命中时 MRR 取 1/3、单金标 nDCG 取折损增益
		/// </summary>
		[Fact]
		public void Third_rank_hit_discounts_scores()
		{
			Assert.Equal(1.0, RankingMetrics.RecallAtK([X, X, A], [A], 5), 6);
			Assert.Equal(1.0 / 3, RankingMetrics.MrrAtK([X, X, A], [A], 5), 6);
			Assert.Equal(1.0 / Math.Log2(4), RankingMetrics.NdcgAtK([X, X, A], [A], 5), 6);
		}

		/// <summary>
		/// 金标多于 topK 时 Recall 按全部金标数做分母
		/// </summary>
		[Fact]
		public void Gold_larger_than_topk_uses_full_gold_as_denominator()
		{
			Assert.Equal(0.0, RankingMetrics.RecallAtK([C, A], [A, B], 1), 6);
			Assert.Equal(0.5, RankingMetrics.RecallAtK([C, A], [A, B], 2), 6);
			Assert.Equal(0.5, RankingMetrics.MrrAtK([C, A], [A, B], 2), 6);
			var expected = (1.0 / Math.Log2(3)) / (1.0 + 1.0 / Math.Log2(3));
			Assert.Equal(expected, RankingMetrics.NdcgAtK([C, A], [A, B], 2), 6);
		}

		/// <summary>
		/// 两条金标分散在答案中时 nDCG 按理想增益归一化
		/// </summary>
		[Fact]
		public void Ndcg_normalizes_by_ideal_gain()
		{
			var dcg = 1.0 / Math.Log2(2) + 1.0 / Math.Log2(4);
			var idcg = 1.0 / Math.Log2(2) + 1.0 / Math.Log2(3);

			Assert.Equal(dcg / idcg, RankingMetrics.NdcgAtK([A, X, B], [A, B], 5), 6);
		}

		/// <summary>
		/// 截断深度之前的排名才计入指标
		/// </summary>
		[Fact]
		public void Ranking_beyond_k_is_ignored()
		{
			Assert.Equal(0.0, RankingMetrics.RecallAtK([X, X, A], [A], 2), 6);
			Assert.Equal(0.0, RankingMetrics.MrrAtK([X, X, A], [A], 2), 6);
			Assert.Equal(0.0, RankingMetrics.NdcgAtK([X, X, A], [A], 2), 6);
			Assert.Equal(0.0, RankingMetrics.RecallAtK([A], [A], 0), 6);
		}

		/// <summary>
		/// 金标为空时三个指标都返回 0，避免除零
		/// </summary>
		[Fact]
		public void Empty_gold_returns_zero()
		{
			Assert.Equal(0.0, RankingMetrics.RecallAtK([A], [], 5), 6);
			Assert.Equal(0.0, RankingMetrics.MrrAtK([A], [], 5), 6);
			Assert.Equal(0.0, RankingMetrics.NdcgAtK([A], [], 5), 6);
		}
	}
}
