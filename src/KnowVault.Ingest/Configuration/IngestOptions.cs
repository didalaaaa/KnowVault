namespace KnowVault.Ingest
{
	/// <summary>
	/// 文档切片参数（P1 走配置绑定；ABP Setting 化留二期）。
	/// 上传白名单/大小上限属应用层契约，见 <c>KnowVault.Domain.DocumentUploadOptions</c>。
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
	}
}
