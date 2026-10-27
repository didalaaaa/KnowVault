namespace KnowVault.Application.Contracts
{
	/// <summary>
	/// 会话摘要
	/// </summary>
	public class ConversationDto
	{
		/// <summary>
		/// 会话 ID
		/// </summary>
		public Guid Id { get; set; }

		/// <summary>
		/// 会话标题
		/// </summary>
		public string Title { get; set; } = string.Empty;

		/// <summary>
		/// 归属知识库（主库）；历史会话为空
		/// </summary>
		public Guid? KnowledgeBaseId { get; set; }

		/// <summary>
		/// 会话提问范围包含的知识库；旧会话回落到 <see cref="KnowledgeBaseId"/> 单值
		/// </summary>
		public IReadOnlyList<Guid> KnowledgeBaseIds { get; set; } = [];

		/// <summary>
		/// 会话是否以「全部知识库」为范围
		/// </summary>
		public bool AllKnowledgeBases { get; set; }

		/// <summary>
		/// 创建时间
		/// </summary>
		public DateTime CreationTime { get; set; }

		/// <summary>
		/// 最后一条消息时间，列表按它倒序
		/// </summary>
		public DateTime? LastMessageAt { get; set; }
	}
}
