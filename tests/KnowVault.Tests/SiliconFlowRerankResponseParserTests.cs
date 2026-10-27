using KnowVault.Ai;
using Xunit;

namespace KnowVault.Tests
{
	/// <summary>
	/// 重排响应解析的契约测试（响应样例取自 SiliconFlow 官方文档）
	/// </summary>
	public class SiliconFlowRerankResponseParserTests
	{
		/// <summary>
		/// 正常响应按下标映射并降序排列
		/// </summary>
		[Fact]
		public void Parse_maps_index_and_orders_by_score_descending()
		{
			const string json = """
			{"id":"rerank-abc","results":[{"index":1,"relevance_score":0.2281},{"index":0,"relevance_score":0.9113}],"meta":{"billed_units":{"search_units":1}}}
			""";

			var results = SiliconFlowRerankResponseParser.Parse(json);

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
			{"id":"rerank-abc","results":[{"relevance_score":0.5},{"index":2},{"index":1,"relevance_score":0.7}]}
			""";

			var results = SiliconFlowRerankResponseParser.Parse(json);

			Assert.Single(results);
			Assert.Equal(1, results[0].Index);
			Assert.Equal(0.7, results[0].Score, 4);
		}

		/// <summary>
		/// 没有 results 节点（例如只有错误码的响应）时返回空列表
		/// </summary>
		[Fact]
		public void Parse_returns_empty_when_results_missing()
		{
			var results = SiliconFlowRerankResponseParser.Parse("""{"code":20012,"message":"Model does not exist"}""");

			Assert.Empty(results);
		}

		/// <summary>
		/// results 为空数组时返回空列表
		/// </summary>
		[Fact]
		public void Parse_returns_empty_for_empty_results()
		{
			var results = SiliconFlowRerankResponseParser.Parse("""{"id":"rerank-abc","results":[]}""");

			Assert.Empty(results);
		}

		/// <summary>
		/// 从 meta.tokens.input_tokens 解析输入 token 用量
		/// </summary>
		[Fact]
		public void ParseInputTokens_reads_meta_tokens()
		{
			const string json = """
			{"id":"rerank-abc","results":[{"index":0,"relevance_score":0.9}],"meta":{"tokens":{"input_tokens":1234,"output_tokens":0}}}
			""";

			Assert.Equal(1234, SiliconFlowRerankResponseParser.ParseInputTokens(json));
		}

		/// <summary>
		/// 响应缺少 meta.tokens 时返回 null
		/// </summary>
		[Fact]
		public void ParseInputTokens_returns_null_when_missing()
		{
			Assert.Null(SiliconFlowRerankResponseParser.ParseInputTokens("""{"id":"rerank-abc","results":[]}"""));
		}
	}
}
