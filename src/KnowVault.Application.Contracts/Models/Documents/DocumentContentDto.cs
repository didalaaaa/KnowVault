namespace KnowVault.Application.Contracts
{
	/// <summary>
	/// 文档原件内容（仅应用层与控制器之间使用）
	/// </summary>
	public class DocumentContentDto
	{
		public Stream Stream { get; set; } = Stream.Null;
		public string ContentType { get; set; } = "application/octet-stream";
		public string FileName { get; set; } = string.Empty;
	}
}
