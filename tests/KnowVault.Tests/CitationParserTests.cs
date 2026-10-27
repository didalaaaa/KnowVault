using KnowVault.Ai;
using Xunit;

namespace KnowVault.Tests
{
	/// <summary>
	/// 引用标记解析的纯函数测试：只识别 [n]，去重并保留首次出现顺序
	/// </summary>
	public class CitationParserTests
	{
		/// <summary>
		/// 多个引用按出现顺序返回
		/// </summary>
		[Fact]
		public void Extracts_citations_in_order()
		{
			Assert.Equal(new[] { 1, 3, 2 }, CitationParser.Parse("结论一[1]，补充[3]，再补充[2]"));
		}

		/// <summary>
		/// 重复引用只保留第一次出现
		/// </summary>
		[Fact]
		public void Duplicate_citations_are_deduplicated()
		{
			Assert.Equal(new[] { 2, 1 }, CitationParser.Parse("[2] 与 [1]，再次 [2]"));
		}

		/// <summary>
		/// 0 与超过三位数的标记不视为引用
		/// </summary>
		[Fact]
		public void Invalid_or_zero_index_is_ignored()
		{
			Assert.Equal(new[] { 1 }, CitationParser.Parse("[0] 与 [1000] 忽略，[1] 保留"));
		}

		/// <summary>
		/// 没有引用标记时返回空列表
		/// </summary>
		[Fact]
		public void Missing_citations_return_empty()
		{
			Assert.Empty(CitationParser.Parse("资料不足，无法回答"));
			Assert.Empty(CitationParser.Parse(null));
			Assert.Empty(CitationParser.Parse("  "));
		}

		/// <summary>
		/// 非数字方括号不参与解析
		/// </summary>
		[Fact]
		public void Non_numeric_brackets_are_ignored()
		{
			Assert.Equal(new[] { 1 }, CitationParser.Parse("[abc] [1] [2a]"));
		}
	}
}
