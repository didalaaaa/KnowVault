namespace KnowVault.Domain
{
	/// <summary>
	/// 文档上传约束（应用层校验用）。
	/// 放在 Domain：上传校验属应用/领域契约，不应让 Application 依赖 Ingest 的解析与消息队列实现。
	/// </summary>
	public class DocumentUploadOptions
	{
		/// <summary>允许上传的扩展名白名单（小写、含点）。</summary>
		public string[] AllowedExtensions { get; set; } =
			[".pdf", ".docx", ".xlsx", ".md", ".markdown", ".html", ".htm"];

		/// <summary>单文件大小上限（MB）。</summary>
		public int MaxFileSizeMb { get; set; } = 50;
	}
}
