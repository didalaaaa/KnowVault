using KnowVault.Domain.Shared;

namespace KnowVault.Ingest
{
	/// <summary>
	/// 文档摄入参数（P1 走配置绑定；ABP Setting 化留二期）。
	/// </summary>
	public class IngestOptions
	{
		/// <summary>
		/// 单个切片的目标长度（单位：字符）。
		/// 中文场景没有可靠 tokenizer，用字符近似 token：约 1 token ≈ 1.6 中文字符。
		/// </summary>
		public int ChunkSizeChars { get; set; } = 1000;

		/// <summary>相邻切片的重叠长度（字符），避免语义被切断。</summary>
		public int ChunkOverlapChars { get; set; } = 120;

		/// <summary>允许上传的扩展名白名单（小写、含点）。</summary>
		public string[] AllowedExtensions { get; set; } =
			[".pdf", ".docx", ".xlsx", ".md", ".markdown", ".html", ".htm"];

		/// <summary>单文件大小上限（MB）。</summary>
		public int MaxFileSizeMb { get; set; } = 50;

		/// <summary>按扩展名推断来源类型。</summary>
		/// <param name="extension">含点的小写扩展名，例如 <c>.docx</c>。</param>
		/// <returns>对应的来源类型；无法识别时为 <c>null</c>。</returns>
		public static DocumentSourceTypeEnum? ResolveSourceType(string extension) => extension switch
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
