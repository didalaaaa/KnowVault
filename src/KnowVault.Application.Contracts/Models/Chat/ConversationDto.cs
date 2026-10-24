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
		/// 归属知识库；历史会话为空
		/// </summary>
		public Guid? KnowledgeBaseId { get; set; }

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
