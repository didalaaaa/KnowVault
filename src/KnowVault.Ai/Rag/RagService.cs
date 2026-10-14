using KnowVault.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace KnowVault.Ai
{
	/// <summary>RAG 编排：问题向量化 → 向量检索 → 组装 Prompt → DeepSeek 生成 → 引用溯源。</summary>
	public class RagService : IRagService, ITransientDependency
	{
		private readonly AiOptions _options;
		private readonly IEmbeddingService _embeddingService;
		private readonly IVectorStore _vectorStore;
		private readonly ILlmChatService _chatService;
		private readonly ILogger<RagService> _logger;

		/// <summary>构造函数。</summary>
		/// <param name="options">AI 配置。</param>
		/// <param name="embeddingService">向量化服务。</param>
		/// <param name="vectorStore">向量库。</param>
		/// <param name="chatService">生成服务。</param>
		/// <param name="logger">日志。</param>
		public RagService(
			IOptions<AiOptions> options,
			IEmbeddingService embeddingService,
			IVectorStore vectorStore,
			ILlmChatService chatService,
			ILogger<RagService> logger)
		{
			_options = options.Value;
			_embeddingService = embeddingService;
			_vectorStore = vectorStore;
			_chatService = chatService;
			_logger = logger;
		}

		/// <inheritdoc />
		public async Task<RagAnswer> AnswerAsync(RagRequest request, CancellationToken cancellationToken = default)
		{
			// top-k 上限 8（TECH_STACK §9：top-k≈5–8）
			var topK = Math.Clamp(request.TopK <= 0 ? _options.TopK : request.TopK, 1, 8);

			var vectors = await _embeddingService.EmbedAsync([request.Question], cancellationToken);
			var hits = await _vectorStore.SearchAsync(
				vectors[0],
				new VectorFilter { TenantId = request.TenantId, KnowledgeBaseId = request.KnowledgeBaseId },
				topK,
				cancellationToken);

			var chunks = hits.Select(hit => new RagRetrievedChunk
			{
				DocumentId = ChunkPayload.ReadGuid(hit.Payload, ChunkPayload.DocumentId),
				DocumentTitle = ChunkPayload.ReadString(hit.Payload, ChunkPayload.DocumentTitle),
				ChunkIndex = ChunkPayload.ReadInt(hit.Payload, ChunkPayload.ChunkIndex),
				HeadingPath = ChunkPayload.ReadString(hit.Payload, ChunkPayload.HeadingPath),
				Content = ChunkPayload.ReadString(hit.Payload, ChunkPayload.Content),
				Score = hit.Score
			}).ToList();

			_logger.LogInformation("检索完成：知识库 {KnowledgeBaseId} 命中 {Count} 个片段（topK={TopK}）", request.KnowledgeBaseId, chunks.Count, topK);

			var (system, user) = RagPromptBuilder.Build(request.Question, chunks);
			var answer = await _chatService.CompleteAsync(system, user, cancellationToken);

			return new RagAnswer
			{
				Answer = answer,
				Citations = [..chunks.OrderByDescending(c => c.Score).Select(c => new RagCitation
				{
					DocumentId = c.DocumentId,
					Title = c.DocumentTitle,
					ChunkIndex = c.ChunkIndex,
					Score = c.Score
				})]
			};
		}
	}
}
