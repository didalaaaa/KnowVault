using KnowVault.Domain.Shared;

namespace KnowVault.Ingest
{
	/// <summary>按格式解析文档为「带标题层级的段落集合」。</summary>
	public interface IDocumentParser
	{
		/// <summary>能处理的来源格式。</summary>
		DocumentSourceTypeEnum SourceType { get; }

		/// <summary>能处理的扩展名（小写、含点）。</summary>
		IReadOnlyList<string> Extensions { get; }

		/// <summary>解析文档。</summary>
		/// <param name="content">文档字节流（须可读、可定位）。</param>
		/// <param name="fileName">原始文件名（用于兜底标题）。</param>
		/// <returns>解析结果。</returns>
		ParsedDocument Parse(Stream content, string fileName);
	}
}
