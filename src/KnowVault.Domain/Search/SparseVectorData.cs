namespace KnowVault.Domain
{
	/// <summary>
	/// 稀疏向量：索引与权重一一对应，索引为稳定哈希值
	/// </summary>
	public sealed class SparseVectorData
	{
		/// <summary>
		/// 词元哈希索引（升序）
		/// </summary>
		public required uint[] Indices { get; init; }

		/// <summary>
		/// 与 <see cref="Indices"/> 一一对应的权重
		/// </summary>
		public required float[] Values { get; init; }

		/// <summary>
		/// 是否没有任何可索引词元
		/// </summary>
		public bool IsEmpty => Indices.Length == 0;
	}
}
