using KnowVault.Domain.Shared;

namespace KnowVault.Application.Contracts
{
	/// <summary>
	/// 会话中的一条消息
	/// </summary>
	public class ChatMessageDto
	{
		/// <summary>
		/// 消息 ID
		/// </summary>
		public Guid Id { get; set; }

		/// <summary>
		/// 角色：用户或助手
		/// </summary>
		public MessageRoleEnum Role { get; set; }

		/// <summary>
		/// 消息正文
		/// </summary>
		public string Content { get; set; } = string.Empty;

		/// <summary>
		/// 引用溯源；无引用时为空列表
		/// </summary>
		public IReadOnlyList<RagCitationDto> Citations { get; set; } = [];

		/// <summary>
		/// 创建时间
		/// </summary>
		public DateTime CreationTime { get; set; }

		/// <summary>
		/// 当前用户对助手消息的反馈；未反馈或用户消息为空
		/// </summary>
		public FeedbackRatingEnum? Feedback { get; set; }

		/// <summary>
		/// 助手回答耗时（毫秒）；用户消息为空
		/// </summary>
		public int? LatencyMs { get; set; }
	}
}
