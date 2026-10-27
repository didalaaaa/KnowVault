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

		/// <summary>
		/// 请求级混合检索开关；为空时使用 <see cref="AiOptions.HybridEnabled"/>
		/// </summary>
		public bool? HybridEnabled { get; set; }

		/// <summary>
		/// 请求级查询改写开关；为空时使用 <see cref="AiOptions.QueryRewriteEnabled"/>
		/// </summary>
		public bool? RewriteEnabled { get; set; }
	}

	/// <summary>
	/// 检索阶段的结果：引用切片与实际参与召回的查询
	/// </summary>
	public class RagRetrievalResult
	{
		/// <summary>
		/// 检索并按需重排后的切片，顺序即引用编号顺序
		/// </summary>
		public required IReadOnlyList<RagRetrievedChunk> Chunks { get; init; }

		/// <summary>
		/// 实际参与召回的查询（第一条为原问题，其余为改写变体）
		/// </summary>
		public required IReadOnlyList<string> RewriteQueries { get; init; }
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
