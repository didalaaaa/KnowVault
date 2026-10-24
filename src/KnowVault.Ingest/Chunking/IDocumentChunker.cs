namespace KnowVault.Ingest
{
	/// <summary>把解析结果切成可向量化的切片。</summary>
	public interface IDocumentChunker
	{
		/// <summary>切片。</summary>
		/// <param name="document">解析后的文档。</param>
		/// <param name="options">本次切片参数（长度与重叠）。</param>
		/// <returns>按顺序排列的切片（Index 从 0 起连续）。</returns>
		IReadOnlyList<TextChunk> Chunk(ParsedDocument document, ChunkOptions options);
	}
}
