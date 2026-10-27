using KnowVault.Domain;
using Xunit;

namespace KnowVault.Tests
{
	/// <summary>
	/// 稀疏向量构建测试：文档侧词频加权、查询侧等权
	/// </summary>
	public class SparseVectorBuilderTests
	{
		/// <summary>
		/// 文档侧权重按 1 + ln(tf) 次线性增长
		/// </summary>
		[Fact]
		public void Document_uses_sublinear_term_frequency()
		{
			var vector = SparseVectorBuilder.BuildDocument("知识 知识 知识");

			Assert.NotNull(vector);
			Assert.Single(vector.Indices);
			Assert.Equal(1 + Math.Log(3), vector.Values[0], precision: 4);
		}

		/// <summary>
		/// 查询侧去重后每个词元权重都是 1.0
		/// </summary>
		[Fact]
		public void Query_uses_unit_weight_and_deduplicates()
		{
			var vector = SparseVectorBuilder.BuildQuery("知识 知识 测试");

			Assert.NotNull(vector);
			Assert.Equal(2, vector.Indices.Length);
			Assert.All(vector.Values, value => Assert.Equal(1f, value));
			Assert.Equal(vector.Indices.Order(), vector.Indices);
		}

		/// <summary>
		/// 文档侧索引按哈希值升序输出
		/// </summary>
		[Fact]
		public void Document_indexes_are_sorted()
		{
			var vector = SparseVectorBuilder.BuildDocument("知识库测试文档");

			Assert.NotNull(vector);
			Assert.Equal(vector.Indices.Order(), vector.Indices);
		}

		/// <summary>
		/// 没有可索引词元时返回空值
		/// </summary>
		[Fact]
		public void Empty_text_returns_null()
		{
			Assert.Null(SparseVectorBuilder.BuildDocument(""));
			Assert.Null(SparseVectorBuilder.BuildDocument("，。！"));
			Assert.Null(SparseVectorBuilder.BuildQuery(null));
		}
	}
}
