namespace KnowVault.Ai
{
	/// <summary>一次 RAG 提问的入参。</summary>
	public class RagRequest
	{
		/// <summary>知识库 ID。</summary>
		public Guid KnowledgeBaseId { get; set; }

		/// <summary>租户 ID（单租户运行时为 Guid.Empty，用于 Qdrant payload 过滤）。</summary>
		public Guid TenantId { get; set; }

		/// <summary>会话 ID（用于日志追踪；P1 不带多轮历史）。</summary>
		public Guid ConversationId { get; set; }

		/// <summary>用户问题。</summary>
		public string Question { get; set; } = default!;

		/// <summary>top-k 检索数；&lt;=0 时用 <see cref="AiOptions.TopK"/>，上限 8。</summary>
		public int TopK { get; set; }

		/// <summary>
		/// 知识库自定义系统提示词；为空时使用内置模板
		/// </summary>
		public string? SystemPromptOverride { get; set; }

		/// <summary>
		/// 历史轮次（时间升序，不含本轮提问）
		/// </summary>
		public IReadOnlyList<LlmChatMessage> History { get; set; } = [];
	}

	/// <summary>RAG 回答。</summary>
	public class RagAnswer
	{
		/// <summary>模型生成的回答（含 [编号] 引用标注）。</summary>
		public string Answer { get; set; } = default!;

		/// <summary>引用溯源。</summary>
		public IReadOnlyList<RagCitation> Citations { get; set; } = [];
	}

	/// <summary>引用溯源。</summary>
	public class RagCitation
	{
		/// <summary>文档 ID。</summary>
		public Guid DocumentId { get; set; }

		/// <summary>文档标题。</summary>
		public string Title { get; set; } = default!;

		/// <summary>切片序号。</summary>
		public int ChunkIndex { get; set; }

		/// <summary>相似度得分。</summary>
		public double Score { get; set; }
	}
}
