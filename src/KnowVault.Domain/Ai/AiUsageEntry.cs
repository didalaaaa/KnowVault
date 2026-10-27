using KnowVault.Domain.Shared;

namespace KnowVault.Domain
{
	/// <summary>
	/// 待记录的一次 AI 调用用量
	/// </summary>
	public class AiUsageEntry
	{
		/// <summary>
		/// 关联知识库；多库/全局问答为空
		/// </summary>
		public Guid? KnowledgeBaseId { get; init; }

		/// <summary>
		/// 关联会话；问答类调用有值
		/// </summary>
		public Guid? ConversationId { get; init; }

		/// <summary>
		/// 同一次问答的关联 ID；摄取调用为空
		/// </summary>
		public Guid? AskId { get; init; }

		/// <summary>
		/// 关联文档；摄取向量化时有值
		/// </summary>
		public Guid? DocumentId { get; init; }

		/// <summary>
		/// AI 调用类型
		/// </summary>
		public AiOperationEnum Operation { get; init; }

		/// <summary>
		/// 实际使用的模型名
		/// </summary>
		public string Model { get; init; } = string.Empty;

		/// <summary>
		/// 输入 token 数
		/// </summary>
		public int PromptTokens { get; init; }

		/// <summary>
		/// 输出 token 数；embedding / rerank 为 0
		/// </summary>
		public int CompletionTokens { get; init; }

		/// <summary>
		/// 该次调用耗时（毫秒）
		/// </summary>
		public int LatencyMs { get; init; }

		/// <summary>
		/// 是否由本地估算 token
		/// </summary>
		public bool IsEstimated { get; init; }
	}
}
