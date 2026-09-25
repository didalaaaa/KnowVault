namespace KnowVault.Domain.Shared
{

	/// <summary>
	/// 文档来源格式（P0 骨架先声明类型，逐格式解析在 P1 实现）。
	/// </summary>
	public enum DocumentSourceTypeEnum
	{
		/// <summary>PDF（PdfPig 解析）</summary>
		Pdf = 0,

		/// <summary>Word (.docx)（DocumentFormat.OpenXml 解析）</summary>
		Word = 1,

		/// <summary>Excel (.xlsx)（NPOI / ClosedXML 解析）</summary>
		Excel = 2,

		/// <summary>Markdown（Markdig 解析）</summary>
		Markdown = 3,

		/// <summary>HTML（HtmlAgilityPack 解析）</summary>
		Html = 4
	}

}
