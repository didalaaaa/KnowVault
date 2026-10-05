namespace KnowVault.Ingest
{
	/// <summary>解析后的文档（只保留正文结构，丢弃版式）。</summary>
	public class ParsedDocument
	{
		/// <summary>文档标题（取不到时用文件名）。</summary>
		public string Title { get; set; } = string.Empty;

		/// <summary>按阅读顺序排列的段落。</summary>
		public List<ParsedSection> Sections { get; set; } = [];
	}

	/// <summary>带标题层级路径的段落。</summary>
	public class ParsedSection
	{
		/// <summary>标题层级路径，例如「第三章 &gt; 3.2 部署」；无标题时为空串。</summary>
		public string HeadingPath { get; set; } = string.Empty;

		/// <summary>段落正文。</summary>
		public string Text { get; set; } = string.Empty;
	}
}
