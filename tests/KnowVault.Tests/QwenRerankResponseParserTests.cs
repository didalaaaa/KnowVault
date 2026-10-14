using KnowVault.Ai;
using Xunit;

namespace KnowVault.Tests
{
	/// <summary>
	/// 重排响应解析的契约测试（响应样例取自 2026-10-14 的真实调用）
	/// </summary>
	public class QwenRerankResponseParserTests
	{
		/// <summary>
		/// 正常响应按下标映射并降序排列
		/// </summary>
		[Fact]
		public void Parse_maps_index_and_orders_by_score_descending()
		{
			const string json = """
			{"output":{"results":[{"index":1,"relevance_score":0.2281},{"index":0,"relevance_score":0.9113}]},"usage":{"total_tokens":60},"request_id":"x"}
			""";

			var results = QwenRerankResponseParser.Parse(json);

			Assert.Equal(2, results.Count);
			Assert.Equal(0, results[0].Index);
			Assert.Equal(0.9113, results[0].Score, 4);
			Assert.Equal(1, results[1].Index);
		}

		/// <summary>
		/// 缺少 index 或 relevance_score 的条目要被跳过，而不是抛异常
		/// </summary>
		[Fact]
		public void Parse_skips_incomplete_items()
		{
			const string json = """
			{"output":{"results":[{"relevance_score":0.5},{"index":2},{"index":1,"relevance_score":0.7}]}}
			""";

			var results = QwenRerankResponseParser.Parse(json);

			Assert.Single(results);
			Assert.Equal(1, results[0].Index);
			Assert.Equal(0.7, results[0].Score, 4);
		}

		/// <summary>
		/// 没有 output 节点（例如只有错误码的响应）时返回空列表
		/// </summary>
		[Fact]
		public void Parse_returns_empty_when_output_missing()
		{
			var results = QwenRerankResponseParser.Parse("""{"code":"AccessDenied","message":"denied"}""");

			Assert.Empty(results);
		}

		/// <summary>
		/// results 为空数组时返回空列表
		/// </summary>
		[Fact]
		public void Parse_returns_empty_for_empty_results()
		{
			var results = QwenRerankResponseParser.Parse("""{"output":{"results":[]}}""");

			Assert.Empty(results);
		}
	}
}
