namespace KnowVault.Application.Contracts
{
	/// <summary>知识库 DTO。</summary>
	public class KnowledgeBaseDto
	{
		/// <summary>知识库 ID。</summary>
		public Guid Id { get; set; }

		/// <summary>名称。</summary>
		public string Name { get; set; } = string.Empty;

		/// <summary>描述。</summary>
		public string? Description { get; set; }

		/// <summary>已就绪的文档数（列表页展示用）。</summary>
		public int ReadyDocumentCount { get; set; }
	}
}
