using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using KnowVault.Domain;
using KnowVault.Domain.Shared;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace KnowVault.Ai
{
	/// <summary>RAG 编排：问题向量化 → dense/sparse 混合召回 → RRF 融合 → 重排 → 组装 Prompt → DeepSeek 生成 → 引用溯源。</summary>
	public class RagService : IRagService, ITransientDependency
	{
		// 重排候选数上限：一次多取几十条没有收益，还要为 rerank 付费
		private const int MaxCandidateCount = 100;

		private const double MaxHybridWeight = 3;
		private const int MinRrfK = 10;
		private const int MaxRrfK = 100;
		private const int MinPerQueryCandidateCount = 5;
		private const int MaxPerQueryCandidateCount = 30;

		private static readonly ActivitySource ActivitySource = new(KnowVaultTelemetry.ActivitySourceName);

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
			var retrieval = await RetrieveAsync(request, cancellationToken);
			var (system, messages) = RagPromptBuilder.Build(request.Question, retrieval.Chunks, request.History, request.SystemPromptOverride);

			return new RagStreamResult
			{
				Chunks = retrieval.Chunks,
				Deltas = StreamWithEmptyCheckAsync(system, messages, cancellationToken)
			};
		}

		/// <inheritdoc />
		public async Task<RagRetrievalResult> RetrieveAsync(RagRequest request, CancellationToken cancellationToken = default)
		{
			using var activity = ActivitySource.StartActivity("rag.retrieve");
			activity?.SetTag("rag.knowledge_base_id", request.KnowledgeBaseId);

			// top-k 上限 8（TECH_STACK §9：top-k≈5–8）
			var topK = Math.Clamp(request.TopK <= 0 ? _options.TopK : request.TopK, 1, 8);

			// 重排要「宽召回 + 精排」：召回数不小于最终 topK，否则重排没有可选空间
			var candidateCount = _options.RerankEnabled ? Math.Clamp(_options.RerankCandidateCount, topK, MaxCandidateCount) : topK;
			var hybridEnabled = request.HybridEnabled ?? _options.HybridEnabled;
			IReadOnlyList<string> queries = [request.Question];

			activity?.SetTag("rag.top_k", topK);
			activity?.SetTag("rag.candidate_count", candidateCount);
			activity?.SetTag("rag.rerank", _options.RerankEnabled);
			activity?.SetTag("rag.hybrid", hybridEnabled);

			var filter = new VectorFilter
			{
				TenantId = request.TenantId,
				KnowledgeBaseIds = [request.KnowledgeBaseId]
			};

			var chunks = hybridEnabled
				? await RetrieveHybridAsync(queries, filter, candidateCount, cancellationToken)
				: await RetrieveDenseAsync(request.Question, filter, candidateCount, cancellationToken);

			activity?.SetTag("rag.hit_count", chunks.Count);

			_logger.LogInformation(
				"检索完成：知识库 {KnowledgeBaseId} 命中 {Count} 个片段（topK={TopK}，候选 {CandidateCount}，混合 {Hybrid}）",
				request.KnowledgeBaseId, chunks.Count, topK, candidateCount, hybridEnabled);

			// 重排只打分（top_n 传全量），保留前 topK 条由本地负责
			var reranked = await RerankAsync(request.Question, chunks, cancellationToken);
			return new RagRetrievalResult
			{
				Chunks = [.. reranked.Take(topK)],
				RewriteQueries = queries
			};
		}

		/// <summary>
		/// 纯向量召回（P4 行为）：只 embed 原问题并做一次 dense 检索
		/// </summary>
		/// <param name="question">用户问题。</param>
		/// <param name="filter">检索过滤条件。</param>
		/// <param name="limit">召回条数。</param>
		/// <param name="cancellationToken">取消令牌。</param>
		/// <returns>按相似度降序的切片。</returns>
		private async Task<List<RagRetrievedChunk>> RetrieveDenseAsync(string question, VectorFilter filter, int limit, CancellationToken cancellationToken)
		{
			var vectors = await _embeddingService.EmbedAsync([question], cancellationToken);
			var hits = await _vectorStore.SearchAsync(vectors[0], filter, limit, cancellationToken);

			return [.. hits.Select(ToChunk)];
		}

		/// <summary>
		/// 混合召回：每条查询分别走 dense 与 sparse，再按加权 RRF 融合
		/// </summary>
		/// <param name="queries">参与召回的查询（第一条为原问题）。</param>
		/// <param name="filter">检索过滤条件。</param>
		/// <param name="candidateCount">送重排的候选条数。</param>
		/// <param name="cancellationToken">取消令牌。</param>
		/// <returns>按融合得分降序的候选切片。</returns>
		private async Task<List<RagRetrievedChunk>> RetrieveHybridAsync(
			IReadOnlyList<string> queries,
			VectorFilter filter,
			int candidateCount,
			CancellationToken cancellationToken)
		{
			var hybrid = _options.Hybrid;
			var denseWeight = Math.Clamp(hybrid.DenseWeight, 0, MaxHybridWeight);
			var keywordWeight = Math.Clamp(hybrid.KeywordWeight, 0, MaxHybridWeight);
			var variantWeight = Math.Clamp(hybrid.VariantWeight, 0, MaxHybridWeight);
			var rrfK = Math.Clamp(hybrid.RrfK, MinRrfK, MaxRrfK);
			var perQueryLimit = Math.Clamp(hybrid.PerQueryCandidateCount, MinPerQueryCandidateCount, MaxPerQueryCandidateCount);

			var vectors = await _embeddingService.EmbedAsync(queries, cancellationToken);
			var chunksById = new Dictionary<Guid, RagRetrievedChunk>();
			var branches = new List<RrfBranch>();
			var sparseAvailable = keywordWeight > 0;

			for (var index = 0; index < queries.Count; index++)
			{
				var queryWeight = index == 0 ? 1.0 : variantWeight;

				if (denseWeight > 0)
				{
					var denseHits = await _vectorStore.SearchAsync(vectors[index], filter, perQueryLimit, cancellationToken);
					branches.Add(new RrfBranch
					{
						Ids = [.. denseHits.Select(hit => hit.Id)],
						Weight = denseWeight,
						QueryWeight = queryWeight
					});
					RememberHits(chunksById, denseHits);
				}

				if (!sparseAvailable) continue;

				var sparseQuery = SparseVectorBuilder.BuildQuery(queries[index]);
				// 问题切不出词元时关键词分支没有意义，直接跳过
				if (sparseQuery is null) continue;

				try
				{
					var sparseHits = await _vectorStore.SearchSparseAsync(sparseQuery, filter, perQueryLimit, cancellationToken);
					branches.Add(new RrfBranch
					{
						Ids = [.. sparseHits.Select(hit => hit.Id)],
						Weight = keywordWeight,
						QueryWeight = queryWeight
					});
					RememberHits(chunksById, sparseHits);
				}
				catch (Exception ex) when (ex is not OperationCanceledException)
				{
					// 关键词召回故障只影响召回率，不能拖垮整次问答
					_logger.LogWarning(ex, "稀疏检索失败，本次只使用向量召回");
					sparseAvailable = false;
				}
			}

			if (branches.Count == 0) return [];

			return [.. ReciprocalRankFusion.Fuse(branches, rrfK)
				.Take(candidateCount)
				.Select(candidate => chunksById[candidate.Id].WithScore(candidate.Score))];
		}

		/// <summary>
		/// 把检索命中映射为切片，并保留每个切片 ID 的首条命中
		/// </summary>
		/// <param name="chunksById">切片 ID 到切片的映射。</param>
		/// <param name="hits">检索命中。</param>
		private static void RememberHits(Dictionary<Guid, RagRetrievedChunk> chunksById, IReadOnlyList<VectorSearchHit> hits)
		{
			foreach (var hit in hits) chunksById.TryAdd(hit.Id, ToChunk(hit));
		}

		/// <summary>
		/// 从 Qdrant payload 还原一个切片
		/// </summary>
		/// <param name="hit">检索命中。</param>
		/// <returns>切片。</returns>
		private static RagRetrievedChunk ToChunk(VectorSearchHit hit) => new()
		{
			DocumentId = ChunkPayload.ReadGuid(hit.Payload, ChunkPayload.DocumentId),
			DocumentTitle = ChunkPayload.ReadString(hit.Payload, ChunkPayload.DocumentTitle),
			ChunkIndex = ChunkPayload.ReadInt(hit.Payload, ChunkPayload.ChunkIndex),
			HeadingPath = ChunkPayload.ReadString(hit.Payload, ChunkPayload.HeadingPath),
			Content = ChunkPayload.ReadString(hit.Payload, ChunkPayload.Content),
			Score = hit.Score
		};

		/// <summary>
		/// 包一层空回答检查，让流式与非流式两条路径都得到同样的保护
		/// </summary>
		/// <param name="systemPrompt">系统提示词。</param>
		/// <param name="messages">完整对话消息。</param>
		/// <param name="cancellationToken">取消令牌。</param>
		/// <returns>增量文本流。</returns>
		/// <exception cref="AiCallException">模型没有产出任何文本。</exception>
		private async IAsyncEnumerable<string> StreamWithEmptyCheckAsync(string systemPrompt, IReadOnlyList<LlmChatMessage> messages, [EnumeratorCancellation] CancellationToken cancellationToken)
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
		/// <param name="cancellationToken">取消令牌。</param>
		/// <returns>按相关性降序的切片；重排不可用时返回原列表。</returns>
		private async Task<List<RagRetrievedChunk>> RerankAsync(string question, List<RagRetrievedChunk> chunks, CancellationToken cancellationToken)
		{
			if (!_options.RerankEnabled || chunks.Count <= 1) return chunks;

			try
			{
				var documents = chunks.Select(chunk => chunk.Content).ToList();
				var results = await _rerankService.RerankAsync(question, documents, cancellationToken);
				if (results.Count == 0) return chunks;

				return [.. results
					.Where(result => result.Index >= 0 && result.Index < chunks.Count)
					.Select(result => chunks[result.Index].WithScore(result.Score))];
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
