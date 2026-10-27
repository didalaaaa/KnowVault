namespace KnowVault.Application.Contracts
{
	/// <summary>
	/// 流式问答的一帧事件
	/// </summary>
	public class QaStreamEventDto
	{
		/// <summary>
		/// 事件类型
		/// </summary>
		public QaStreamEventKindEnum Kind { get; set; }

		/// <summary>
		/// 会话 ID，仅首帧携带
		/// </summary>
		public Guid? ConversationId { get; set; }

		/// <summary>
		/// 增量文本，仅 Delta 帧携带
		/// </summary>
		public string? Delta { get; set; }

		/// <summary>
		/// 引用列表，仅首帧携带（检索阶段已确定）
		/// </summary>
		public IReadOnlyList<RagCitationDto> Citations { get; set; } = [];

		/// <summary>
		/// 本次提问实际使用的知识库 ID，仅首帧携带；「全部知识库」时为解析后的集合
		/// </summary>
		public IReadOnlyList<Guid> KnowledgeBaseIds { get; set; } = [];

		/// <summary>
		/// 本轮用户消息 ID，仅首帧携带；重新生成时为空
		/// </summary>
		public Guid? UserMessageId { get; set; }

		/// <summary>
		/// 助手消息 ID，仅完成帧携带（用于反馈）
		/// </summary>
		public Guid? AssistantMessageId { get; set; }

		/// <summary>
		/// 生成耗时（毫秒），仅完成帧携带
		/// </summary>
		public int? LatencyMs { get; set; }

		/// <summary>
		/// 本次提问实际使用的混合检索开关，仅首帧携带
		/// </summary>
		public bool HybridEnabled { get; set; }

		/// <summary>
		/// 实际参与召回的改写变体（不含原问题），仅首帧携带
		/// </summary>
		public IReadOnlyList<string> RewriteQueries { get; set; } = [];
	}
}
