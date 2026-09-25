namespace KnowVault.Ai
{

	/// <summary>一次 RAG 提问的入参。</summary>
	public class RagRequest
	{
		public Guid KnowledgeBaseId { get; set; }

		public Guid ConversationId { get; set; }

		public string Question { get; set; } = default!;

		/// <summary>top-k 检索数。</summary>
		public int TopK { get; set; } = 5;
	}

	/// <summary>RAG 回答。</summary>
	public class RagAnswer
	{
		public string Answer { get; set; } = default!;

		public IReadOnlyList<RagCitation> Citations { get; set; } = Array.Empty<RagCitation>();

		public static RagAnswer Placeholder(string answer) => new() { Answer = answer };
	}

	/// <summary>引用溯源。</summary>
	public class RagCitation
	{
		public Guid DocumentId { get; set; }

		public string Title { get; set; } = default!;

		public int ChunkIndex { get; set; }

		public double Score { get; set; }
	}

}
