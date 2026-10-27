using KnowVault.Ai;
using Xunit;

namespace KnowVault.Tests
{
	/// <summary>
	/// token 估算测试：CJK 一字一 token，其它非空白字符四字符一 token 向上取整
	/// </summary>
	public class TokenEstimatorTests
	{
		/// <summary>
		/// 纯中文按字符数估算
		/// </summary>
		[Fact]
		public void Counts_cjk_as_one_token_per_character()
		{
			Assert.Equal(2, TokenEstimator.Estimate("你好"));
			Assert.Equal(5, TokenEstimator.Estimate("知识库检索"));
		}

		/// <summary>
		/// 英文按四字符一 token 向上取整
		/// </summary>
		[Fact]
		public void Counts_latin_as_four_characters_per_token()
		{
			Assert.Equal(1, TokenEstimator.Estimate("a"));
			Assert.Equal(2, TokenEstimator.Estimate("hello"));
			Assert.Equal(3, TokenEstimator.Estimate("hello world"));
		}

		/// <summary>
		/// 中英混合分别计数后相加
		/// </summary>
		[Fact]
		public void Counts_mixed_text()
		{
			Assert.Equal(4, TokenEstimator.Estimate("你好 world"));
			Assert.Equal(3, TokenEstimator.Estimate("RAG 检索"));
		}

		/// <summary>
		/// 空串、空白与 null 都返回 0
		/// </summary>
		[Fact]
		public void Empty_text_returns_zero()
		{
			Assert.Equal(0, TokenEstimator.Estimate(null));
			Assert.Equal(0, TokenEstimator.Estimate(""));
			Assert.Equal(0, TokenEstimator.Estimate(" \t\r\n"));
		}
	}
}
