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

		/// <summary>切片长度（字符）；为空表示使用全局配置。</summary>
		public int? ChunkSizeChars { get; set; }

		/// <summary>切片重叠（字符）；为空表示使用全局配置。</summary>
		public int? ChunkOverlapChars { get; set; }

		/// <summary>检索条数；为空表示使用全局配置。</summary>
		public int? TopK { get; set; }

		/// <summary>自定义系统提示词；为空表示使用内置模板。</summary>
		public string? SystemPrompt { get; set; }
	}
}
