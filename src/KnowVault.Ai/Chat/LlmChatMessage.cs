using KnowVault.Domain.Shared;

namespace KnowVault.Ai
{
	/// <summary>
	/// 交给大模型的一轮对话消息
	/// </summary>
	public class LlmChatMessage
	{
		/// <summary>
		/// 角色（用户或助手）
		/// </summary>
		public MessageRoleEnum Role { get; set; }

		/// <summary>
		/// 消息正文
		/// </summary>
		public string Content { get; set; } = string.Empty;
	}
}
