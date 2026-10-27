namespace KnowVault.Application.Contracts
{

	/// <summary>引用溯源 DTO。</summary>
	public class RagCitationDto
	{
		public Guid DocumentId { get; set; }

		/// <summary>
		/// 引用所属知识库；旧会话引用为空 GUID
		/// </summary>
		public Guid KnowledgeBaseId { get; set; }

		/// <summary>
		/// 引用所属知识库名称；旧会话引用为空
		/// </summary>
		public string? KnowledgeBaseName { get; set; }

		public string Title { get; set; } = string.Empty;
		public int ChunkIndex { get; set; }
		public double Score { get; set; }

		/// <summary>
		/// 命中片段正文摘要（前 300 字符）
		/// </summary>
		public string? Snippet { get; set; }

		/// <summary>
		/// 标题层级路径，可为空
		/// </summary>
		public string? HeadingPath { get; set; }
	}

}
