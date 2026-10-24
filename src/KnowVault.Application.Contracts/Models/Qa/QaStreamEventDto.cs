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
	}
}
