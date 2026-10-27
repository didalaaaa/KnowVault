using System.Text;

namespace KnowVault.Domain
{
	/// <summary>
	/// 把文本转换为 Qdrant 稀疏向量：文档侧按词频加权，查询侧唯一词元等权
	/// </summary>
	public static class SparseVectorBuilder
	{
		/// <summary>
		/// 构建文档侧稀疏向量，权重为 <c>1 + ln(tf)</c>
		/// </summary>
		/// <param name="text">索引文本（标题 + 正文）</param>
		/// <returns>稀疏向量；没有可索引词元时返回 <c>null</c></returns>
		public static SparseVectorData? BuildDocument(string? text)
		{
			var tokens = KeywordTokenizer.Tokenize(text);
			if (tokens.Count == 0) return null;

			var frequencies = new Dictionary<uint, int>();
			foreach (var token in tokens)
			{
				var index = HashToken(token);
				frequencies[index] = frequencies.GetValueOrDefault(index) + 1;
			}

			return Create(frequencies.Select(pair => (pair.Key, (float)(1 + Math.Log(pair.Value)))));
		}

		/// <summary>
		/// 构建查询侧稀疏向量，每个唯一词元权重为 1.0
		/// </summary>
		/// <param name="text">查询文本</param>
		/// <returns>稀疏向量；没有可索引词元时返回 <c>null</c></returns>
		public static SparseVectorData? BuildQuery(string? text)
		{
			var tokens = KeywordTokenizer.Tokenize(text);
			if (tokens.Count == 0) return null;

			var indices = tokens.Select(HashToken).Distinct().Order().ToArray();
			return new SparseVectorData
			{
				Indices = indices,
				Values = Enumerable.Repeat(1f, indices.Length).ToArray()
			};
		}

		/// <summary>
		/// 计算词元的 FNV-1a 32 位哈希，并映射到 <c>1..uint.MaxValue-1</c>
		/// </summary>
		/// <param name="token">归一化后的词元</param>
		/// <returns>跨进程稳定的非零索引</returns>
		public static uint HashToken(string token)
		{
			const uint offsetBasis = 2166136261;
			const uint prime = 16777619;

			var hash = offsetBasis;
			foreach (var value in Encoding.UTF8.GetBytes(token))
			{
				hash ^= value;
				hash *= prime;
			}

			return hash % (uint.MaxValue - 1) + 1;
		}

		private static SparseVectorData Create(IEnumerable<(uint Index, float Value)> items)
		{
			var ordered = items.OrderBy(item => item.Index).ToArray();
			return new SparseVectorData
			{
				Indices = ordered.Select(item => item.Index).ToArray(),
				Values = ordered.Select(item => item.Value).ToArray()
			};
		}
	}
}
