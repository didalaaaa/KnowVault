namespace KnowVault.Application.Contracts
{
	/// <summary>
	/// 文档切片 DTO
	/// </summary>
	public class DocumentChunkDto
	{
		public Guid Id { get; set; }
		public int ChunkIndex { get; set; }
		public string Content { get; set; } = string.Empty;

		/// <summary>标题层级路径，历史切片为空</summary>
		public string? HeadingPath { get; set; }
	}
}
