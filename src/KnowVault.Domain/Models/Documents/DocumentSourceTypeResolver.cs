using KnowVault.Domain.Shared;

namespace KnowVault.Domain
{
	/// <summary>按扩展名推断文档来源类型（P1 的「网页」指上传 .html 文件，不含 URL 抓取）。</summary>
	public static class DocumentSourceTypeResolver
	{
		/// <summary>推断来源类型。</summary>
		/// <param name="extension">含点的小写扩展名，例如 <c>.docx</c>。</param>
		/// <returns>对应的来源类型；无法识别时为 <c>null</c>。</returns>
		public static DocumentSourceTypeEnum? FromExtension(string extension) => extension switch
		{
			".pdf" => DocumentSourceTypeEnum.Pdf,
			".docx" => DocumentSourceTypeEnum.Word,
			".xlsx" => DocumentSourceTypeEnum.Excel,
			".md" or ".markdown" => DocumentSourceTypeEnum.Markdown,
			".html" or ".htm" => DocumentSourceTypeEnum.Html,
			_ => null
		};
	}
}
