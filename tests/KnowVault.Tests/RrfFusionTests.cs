using KnowVault.Ai;
using Xunit;

namespace KnowVault.Tests
{
	/// <summary>
	/// 加权 RRF 融合的纯函数测试：只使用排名，权重与查询权重相乘
	/// </summary>
	public class RrfFusionTests
	{
		private static readonly Guid A = Guid.Parse("00000000-0000-0000-0000-000000000001");
		private static readonly Guid B = Guid.Parse("00000000-0000-0000-0000-000000000002");
		private static readonly Guid C = Guid.Parse("00000000-0000-0000-0000-000000000003");

		/// <summary>
		/// 单分支融合保持原顺序，得分为 1/(k+rank)
		/// </summary>
		[Fact]
		public void Single_branch_preserves_order()
		{
			var candidates = ReciprocalRankFusion.Fuse([new RrfBranch { Ids = [A, B, C] }], 60);

			Assert.Equal(new[] { A, B, C }, candidates.Select(candidate => candidate.Id));
			Assert.Equal(1.0 / 61, candidates[0].Score, 6);
			Assert.Equal(1, candidates[0].BestRank);
		}

		/// <summary>
		/// 多路命中的文档累加分值并取最佳排名
		/// </summary>
		[Fact]
		public void Multi_branch_hits_accumulate_scores()
		{
			var candidates = ReciprocalRankFusion.Fuse(
				[new RrfBranch { Ids = [A, B] }, new RrfBranch { Ids = [B, C] }],
				60);

			Assert.Equal(new[] { B, A, C }, candidates.Select(candidate => candidate.Id));
			Assert.Equal(1.0 / 61 + 1.0 / 62, candidates[0].Score, 6);
			Assert.Equal(1, candidates[0].BestRank);
		}

		/// <summary>
		/// 分支权重与查询权重都会乘到贡献分上
		/// </summary>
		[Fact]
		public void Branch_and_query_weights_multiply()
		{
			var candidates = ReciprocalRankFusion.Fuse(
				[new RrfBranch { Ids = [A], Weight = 2, QueryWeight = 0.5 }],
				60);

			Assert.Equal(1.0 / 61, candidates[0].Score, 6);
		}

		/// <summary>
		/// 同一分支内重复 ID 只取首次排名，不重复计分
		/// </summary>
		[Fact]
		public void Duplicate_ids_in_same_branch_count_once()
		{
			var candidates = ReciprocalRankFusion.Fuse([new RrfBranch { Ids = [A, A, B] }], 60);

			Assert.Equal(1.0 / 61, candidates[0].Score, 6);
			Assert.Equal(1, candidates[0].BestRank);
			Assert.Equal(1.0 / 63, candidates[1].Score, 6);
			Assert.Equal(3, candidates[1].BestRank);
		}

		/// <summary>
		/// 同分时按最佳排名升序，再按 ID 升序稳定排序
		/// </summary>
		[Fact]
		public void Ties_order_by_best_rank_then_id()
		{
			var candidates = ReciprocalRankFusion.Fuse(
				[new RrfBranch { Ids = [B] }, new RrfBranch { Ids = [A] }],
				60);

			Assert.Equal(new[] { A, B }, candidates.Select(candidate => candidate.Id));
		}

		/// <summary>
		/// 空输入返回空结果
		/// </summary>
		[Fact]
		public void Empty_input_returns_empty()
		{
			Assert.Empty(ReciprocalRankFusion.Fuse([], 60));
			Assert.Empty(ReciprocalRankFusion.Fuse([new RrfBranch { Ids = [] }], 60));
		}

		/// <summary>
		/// RrfK 小于 1 时按 1 处理，避免除以 0
		/// </summary>
		[Fact]
		public void RrfK_is_clamped_to_at_least_one()
		{
			var candidates = ReciprocalRankFusion.Fuse([new RrfBranch { Ids = [A] }], 0);

			Assert.Equal(0.5, candidates[0].Score, 6);
		}
	}
}
