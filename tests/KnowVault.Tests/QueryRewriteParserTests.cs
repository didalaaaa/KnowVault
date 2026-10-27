using KnowVault.Ai;
using Xunit;

namespace KnowVault.Tests
{
	/// <summary>
	/// 查询改写解析测试：宽松提取 JSON 数组，非法输出一律退化为空列表
	/// </summary>
	public class QueryRewriteParserTests
	{
		/// <summary>
		/// 允许 ```json 代码块包裹
		/// </summary>
		[Fact]
		public void Parses_json_code_block()
		{
			var queries = QueryRewriteParser.Parse("```json\n[\"替代问题\",\"英文 variant\"]\n```", "原问题", 2);

			Assert.Equal(new[] { "替代问题", "英文 variant" }, queries);
		}

		/// <summary>
		/// 数组前后带一句解释时仍能提取
		/// </summary>
		[Fact]
		public void Parses_array_with_surrounding_text()
		{
			var queries = QueryRewriteParser.Parse("以下是改写结果：[\"替代问题\",\"另一个问题\"]", "原问题", 2);

			Assert.Equal(new[] { "替代问题", "另一个问题" }, queries);
		}

		/// <summary>
		/// 非法 JSON 返回空列表
		/// </summary>
		[Fact]
		public void Invalid_json_returns_empty()
		{
			Assert.Empty(QueryRewriteParser.Parse("[\"未闭合\"", "原问题", 2));
			Assert.Empty(QueryRewriteParser.Parse("not json", "原问题", 2));
		}

		/// <summary>
		/// 非字符串数组返回空列表
		/// </summary>
		[Fact]
		public void Non_string_array_returns_empty()
		{
			Assert.Empty(QueryRewriteParser.Parse("{\"queries\":[1,2]}", "原问题", 2));
			Assert.Empty(QueryRewriteParser.Parse("[\"替代问题\",1]", "原问题", 2));
		}

		/// <summary>
		/// 重复项忽略大小写，只保留第一次出现
		/// </summary>
		[Fact]
		public void Deduplicates_ignoring_case()
		{
			var queries = QueryRewriteParser.Parse("[\"Alpha\",\"alpha\",\"Beta\"]", "原问题", 3);

			Assert.Equal(new[] { "Alpha", "Beta" }, queries);
		}

		/// <summary>
		/// 单条长度必须在 2–120 之间
		/// </summary>
		[Fact]
		public void Drops_items_outside_length_range()
		{
			var tooLong = new string('长', 121);
			var maxLength = new string('长', 120);

			var queries = QueryRewriteParser.Parse($"[\"单\",\"{maxLength}\",\"{tooLong}\"]", "原问题", 3);

			Assert.Equal(new[] { maxLength }, queries);
		}

		/// <summary>
		/// 与原问题相同的项会被丢弃（忽略大小写与首尾空白）
		/// </summary>
		[Fact]
		public void Drops_item_equal_to_original()
		{
			var queries = QueryRewriteParser.Parse("[\" 原问题 \",\"替代问题\"]", "原问题", 2);

			Assert.Equal(new[] { "替代问题" }, queries);
		}

		/// <summary>
		/// 超过上限时只保留前 MaxVariants 条
		/// </summary>
		[Fact]
		public void Trims_to_max_variants()
		{
			var queries = QueryRewriteParser.Parse("[\"第一条\",\"第二条\",\"第三条\"]", "原问题", 2);

			Assert.Equal(new[] { "第一条", "第二条" }, queries);
		}

		/// <summary>
		/// 空输入或上限为 0 时返回空列表
		/// </summary>
		[Fact]
		public void Empty_input_returns_empty()
		{
			Assert.Empty(QueryRewriteParser.Parse(null, "原问题", 2));
			Assert.Empty(QueryRewriteParser.Parse("   ", "原问题", 2));
			Assert.Empty(QueryRewriteParser.Parse("[\"替代问题\"]", "原问题", 0));
		}
	}
}
