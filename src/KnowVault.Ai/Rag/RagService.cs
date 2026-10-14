using System.Runtime.CompilerServices;
using System.Text;
using KnowVault.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace KnowVault.Ai
{
	/// <summary>RAG 编排：问题向量化 → 向量检索 → 重排 → 组装 Prompt → DeepSeek 生成 → 引用溯源。</summary>
	public class RagService : IRagService, ITransientDependency
	{
		// 重排候选数上限：一次多取几十条没有收益，还要为 rerank 付费
		private const int MaxCandidateCount = 100;

		private readonly AiOptions _options;
		private readonly IEmbeddingService _embeddingService;
		private readonly IVectorStore _vectorStore;
		private readonly ILlmChatService _chatService;
		private readonly IRerankService _rerankService;
		private readonly ILogger<RagService> _logger;

		/// <summary>
		/// 构造函数
		/// </summary>
		/// <param name="options">AI 配置</param>
		/// <param name="embeddingService">向量化服务</param>
		/// <param name="vectorStore">向量库</param>
		/// <param name="chatService">生成服务</param>
		/// <param name="rerankService">重排服务</param>
		/// <param name="logger">日志</param>
		public RagService(
			IOptions<AiOptions> options,
			IEmbeddingService embeddingService,
			IVectorStore vectorStore,
			ILlmChatService chatService,
			IRerankService rerankService,
			ILogger<RagService> logger)
		{
			_options = options.Value;
			_embeddingService = embeddingService;
			_vectorStore = vectorStore;
			_chatService = chatService;
			_rerankService = rerankService;
			_logger = logger;
		}

		/// <inheritdoc />
		public async Task<RagAnswer> AnswerAsync(RagRequest request, CancellationToken cancellationToken = default)
		{
			var streamResult = await AnswerStreamAsync(request, cancellationToken);

			// 一次性回答与流式回答共用同一条生成路径，避免两套实现行为漂移
			var builder = new StringBuilder();
			await foreach (var delta in streamResult.Deltas.WithCancellation(cancellationToken))
			{
				builder.Append(delta);
			}

			return new RagAnswer
			{
				Answer = builder.ToString().Trim(),
				Citations = [..streamResult.Chunks.Select(chunk => new RagCitation
				{
					DocumentId = chunk.DocumentId,
					Title = chunk.DocumentTitle,
					ChunkIndex = chunk.ChunkIndex,
					Score = chunk.Score
				})]
			};
		}

		/// <inheritdoc />
		public async Task<RagStreamResult> AnswerStreamAsync(RagRequest request, CancellationToken cancellationToken = default)
		{
			var chunks = await RetrieveAsync(request, cancellationToken);
			var (system, messages) = RagPromptBuilder.Build(request.Question, chunks, request.History);

			return new RagStreamResult
			{
				Chunks = chunks,
				Deltas = StreamWithEmptyCheckAsync(system, messages, cancellationToken)
			};
		}

		/// <summary>
		/// 检索（按需重排）并返回引用切片，顺序即引用编号顺序
		/// </summary>
		/// <param name="request">提问入参。</param>
		/// <param name="cancellationToken">取消令牌。</param>
		/// <returns>引用切片。</returns>
		private async Task<IReadOnlyList<RagRetrievedChunk>> RetrieveAsync(RagRequest request, CancellationToken cancellationToken)
		{
			// top-k 上限 8（TECH_STACK §9：top-k≈5–8）
			var topK = Math.Clamp(request.TopK <= 0 ? _options.TopK : request.TopK, 1, 8);

			// 重排要「宽召回 + 精排」：召回数不小于最终 topK，否则重排没有可选空间
			var candidateCount = _options.RerankEnabled ? Math.Clamp(_options.RerankCandidateCount, topK, MaxCandidateCount) : topK;

			var vectors = await _embeddingService.EmbedAsync([request.Question], cancellationToken);
			var hits = await _vectorStore.SearchAsync(
				vectors[0],
				new VectorFilter { TenantId = request.TenantId, KnowledgeBaseId = request.KnowledgeBaseId },
				candidateCount,
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

			_logger.LogInformation(
				"检索完成：知识库 {KnowledgeBaseId} 命中 {Count} 个片段（topK={TopK}，候选 {CandidateCount}）",
				request.KnowledgeBaseId, chunks.Count, topK, candidateCount);

			var reranked = await RerankAsync(request.Question, chunks, topK, cancellationToken);
			return reranked.Take(topK).ToList();
		}

		/// <summary>
		/// 包一层空回答检查，让流式与非流式两条路径都得到同样的保护
		/// </summary>
		/// <param name="systemPrompt">系统提示词。</param>
		/// <param name="messages">完整对话消息。</param>
		/// <param name="cancellationToken">取消令牌。</param>
		/// <returns>增量文本流。</returns>
		/// <exception cref="AiCallException">模型没有产出任何文本。</exception>
		private async IAsyncEnumerable<string> StreamWithEmptyCheckAsync(
			string systemPrompt,
			IReadOnlyList<LlmChatMessage> messages,
			[EnumeratorCancellation] CancellationToken cancellationToken)
		{
			var totalLength = 0;
			await foreach (var delta in _chatService.CompleteStreamAsync(systemPrompt, messages, cancellationToken).WithCancellation(cancellationToken))
			{
				totalLength += delta.Length;
				yield return delta;
			}

			if (totalLength == 0) throw new AiCallException("DeepSeek 返回了空回答。");
		}

		/// <summary>
		/// 调重排服务对候选切片重排，失败时退回向量检索顺序
		/// </summary>
		/// <param name="question">用户问题。</param>
		/// <param name="chunks">向量检索得到的候选切片（按相似度降序）。</param>
		/// <param name="topK">重排后需要保留的条数。</param>
		/// <param name="cancellationToken">取消令牌。</param>
		/// <returns>重排后的切片；重排不可用时返回原列表。</returns>
		private async Task<List<RagRetrievedChunk>> RerankAsync(string question, List<RagRetrievedChunk> chunks, int topK, CancellationToken cancellationToken)
		{
			if (!_options.RerankEnabled || chunks.Count <= 1) return chunks;

			try
			{
				var documents = chunks.Select(chunk => chunk.Content).ToList();
				var results = await _rerankService.RerankAsync(question, documents, topK, cancellationToken);
				if (results.Count == 0) return chunks;

				return results
					.Where(result => result.Index >= 0 && result.Index < chunks.Count)
					.Select(result => chunks[result.Index].WithScore(result.Score))
					.ToList();
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				// 重排是锦上添花：调用失败不能拖垮问答，退回向量分顺序即可
				_logger.LogWarning(ex, "重排失败，退回向量检索顺序");
				return chunks;
			}
		}
	}
}
