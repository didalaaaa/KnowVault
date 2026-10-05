namespace KnowVault.Ingest
{
	/// <summary>一个待向量化的切片。</summary>
	public class TextChunk
	{
		/// <summary>切片序号（0 起）。</summary>
		public int Index { get; set; }

		/// <summary>切片正文。</summary>
		public string Content { get; set; } = string.Empty;

		/// <summary>标题层级路径（用于引用溯源）。</summary>
		public string HeadingPath { get; set; } = string.Empty;
	}
}
