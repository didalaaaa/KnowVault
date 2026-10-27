using KnowVault.Domain;
using Xunit;

namespace KnowVault.Tests
{
	/// <summary>
	/// 关键词切词器的纯函数测试
	/// </summary>
	public class KeywordTokenizerTests
	{
		/// <summary>
		/// 中英混合文本按片段切分，英文保留长度不小于 2 的词元
		/// </summary>
		[Fact]
		public void Mixed_chinese_and_latin_is_tokenized()
		{
			var tokens = KeywordTokenizer.Tokenize("KnowVault 知识库 P5");

			Assert.Equal(["knowvault", "知识", "识库", "p5"], tokens);
		}

		/// <summary>
		/// 全角字母与全角空格先做 NFKC 归一化
		/// </summary>
		[Fact]
		public void Full_width_characters_are_normalized()
		{
			var tokens = KeywordTokenizer.Tokenize("ＫＮＯＷ　知识库");

			Assert.Equal(["know", "知识", "识库"], tokens);
		}

		/// <summary>
		/// 停用词只丢弃自身，不影响相邻二元组
		/// </summary>
		[Fact]
		public void Stop_words_are_removed()
		{
			var tokens = KeywordTokenizer.Tokenize("什么是知识库");

			Assert.DoesNotContain("什么", tokens);
			Assert.Contains("知识", tokens);
			Assert.Contains("识库", tokens);
		}

		/// <summary>
		/// 单个 CJK 字保留为独立词元
		/// </summary>
		[Fact]
		public void Single_cjk_character_is_kept()
		{
			var tokens = KeywordTokenizer.Tokenize("库");

			Assert.Equal(["库"], tokens);
		}

		/// <summary>
		/// 单个拉丁字符不产生词元
		/// </summary>
		[Fact]
		public void Single_latin_character_is_dropped()
		{
			var tokens = KeywordTokenizer.Tokenize("a b cd");

			Assert.Equal(["cd"], tokens);
		}

		/// <summary>
		/// 标点与空白不产生词元
		/// </summary>
		[Fact]
		public void Punctuation_is_ignored()
		{
			var tokens = KeywordTokenizer.Tokenize("知识库，测试！");

			Assert.Equal(["知识", "识库", "测试"], tokens);
		}

		/// <summary>
		/// 空文本返回空列表
		/// </summary>
		[Fact]
		public void Empty_or_whitespace_returns_empty()
		{
			Assert.Empty(KeywordTokenizer.Tokenize(null));
			Assert.Empty(KeywordTokenizer.Tokenize("   "));
		}

		/// <summary>
		/// 同一词元的哈希跨调用稳定且映射到非零区间
		/// </summary>
		[Fact]
		public void Hash_is_stable_and_non_zero()
		{
			Assert.Equal(3066253032u, SparseVectorBuilder.HashToken("知识"));
			Assert.Equal(SparseVectorBuilder.HashToken("知识"), SparseVectorBuilder.HashToken("知识"));
			Assert.NotEqual(0u, SparseVectorBuilder.HashToken("知识"));
		}
	}
}
