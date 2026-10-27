using System.Diagnostics;
using KnowVault.Domain;
using KnowVault.Domain.Shared;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

namespace KnowVault.Ingest
{
	/// <summary>
	/// 文档处理管线。失败直接抛出，由 MassTransit 重试；重试耗尽进 error 队列（P3）
	/// </summary>
	public class DocumentProcessor : IDocumentProcessor, ITransientDependency
	{
		private static readonly ActivitySource ActivitySource = new(KnowVaultTelemetry.ActivitySourceName);

		private readonly IRepository<Document, Guid> _documentRepository;
		private readonly IRepository<DocumentChunk, Guid> _chunkRepository;
		private readonly IRepository<KnowledgeBase, Guid> _knowledgeBaseRepository;
		private readonly IDocumentStorage _storage;
		private readonly DocumentParserRegistry _parserRegistry;
		private readonly IDocumentChunker _chunker;
		private readonly IEmbeddingService _embeddingService;
		private readonly IVectorStore _vectorStore;
		private readonly IAiUsageRecorder _usageRecorder;
		private readonly IGuidGenerator _guidGenerator;
		private readonly IOptions<IngestOptions> _ingestOptions;
		private readonly ILogger<DocumentProcessor> _logger;

		/// <summary>构造函数。</summary>
		/// <param name="documentRepository">文档仓储。</param>
		/// <param name="chunkRepository">切片仓储。</param>
		/// <param name="knowledgeBaseRepository">知识库仓储（读取每库切片设置）</param>
		/// <param name="storage">对象存储。</param>
		/// <param name="parserRegistry">解析器注册表。</param>
		/// <param name="chunker">切片器。</param>
		/// <param name="embeddingService">向量化服务。</param>
		/// <param name="vectorStore">向量库。</param>
		/// <param name="usageRecorder">AI 用量记录器。</param>
		/// <param name="guidGenerator">GUID 生成器（ABP 约定）。</param>
		/// <param name="ingestOptions">全局摄入参数（知识库未设置时回落）</param>
		/// <param name="logger">日志。</param>
		public DocumentProcessor(
			IRepository<Document, Guid> documentRepository,
			IRepository<DocumentChunk, Guid> chunkRepository,
			IRepository<KnowledgeBase, Guid> knowledgeBaseRepository,
			IDocumentStorage storage,
			DocumentParserRegistry parserRegistry,
			IDocumentChunker chunker,
			IEmbeddingService embeddingService,
			IVectorStore vectorStore,
			IAiUsageRecorder usageRecorder,
			IGuidGenerator guidGenerator,
			IOptions<IngestOptions> ingestOptions,
			ILogger<DocumentProcessor> logger)
		{
			_documentRepository = documentRepository;
			_chunkRepository = chunkRepository;
			_knowledgeBaseRepository = knowledgeBaseRepository;
			_storage = storage;
			_parserRegistry = parserRegistry;
			_chunker = chunker;
			_embeddingService = embeddingService;
			_vectorStore = vectorStore;
			_usageRecorder = usageRecorder;
			_guidGenerator = guidGenerator;
			_ingestOptions = ingestOptions;
			_logger = logger;
		}

		/// <inheritdoc />
		public async Task ProcessAsync(Guid documentId, CancellationToken cancellationToken = default)
		{
			var document = await _documentRepository.FindAsync(documentId, cancellationToken: cancellationToken);
			if (document is null)
			{
				// 抛错而不是静默返回：最常见的成因是「消息先于上传事务的提交到达」，
				// 交给 MassTransit 的重试策略即可在提交后重新投递（重试耗尽会进 error 队列，不会无声消失）。
				throw new InvalidOperationException($"文档 {documentId} 当前不可见（可能上传事务尚未提交），交由重试策略重新投递。");
			}

			using var activity = ActivitySource.StartActivity("ingest.process");
			activity?.SetTag("document.id", document.Id);
			activity?.SetTag("document.knowledge_base_id", document.KnowledgeBaseId);

			// 失败不再在这里吞掉：交给消费者记录 Failed 后重新抛出，让 MassTransit 真正重试
			await ProcessInternalAsync(document, cancellationToken);
		}

		private async Task ProcessInternalAsync(Document document, CancellationToken cancellationToken)
		{
			if (string.IsNullOrWhiteSpace(document.ObjectStorageKey))
			{
				throw new InvalidOperationException("文档缺少对象存储键，无法解析。");
			}

			document.RecordAttempt();
			document.MarkProcessing();
			await _documentRepository.UpdateAsync(document, autoSave: true, cancellationToken: cancellationToken);

			var extension = Path.GetExtension(document.ObjectStorageKey).ToLowerInvariant();
			var parser = _parserRegistry.Resolve(extension);

			ParsedDocument parsed;
			await using (var stream = await _storage.GetAsync(document.ObjectStorageKey!, cancellationToken))
			{
				parsed = parser.Parse(stream, document.ObjectStorageKey!);
			}

			var chunks = _chunker.Chunk(parsed, await ResolveChunkOptionsAsync(document.KnowledgeBaseId, cancellationToken));
			if (chunks.Count == 0)
			{
				throw new InvalidOperationException("未从文档中解析出任何文本内容。");
			}

			var stopwatch = Stopwatch.StartNew();
			var embedding = await _embeddingService.EmbedAsync(chunks.Select(c => c.Content).ToList(), cancellationToken);
			stopwatch.Stop();
			var vectors = embedding.Vectors;

			// 摄取按文档聚合：一次 EmbedAsync 只记一条，AskId 为空
			await _usageRecorder.RecordAsync(new AiUsageEntry
			{
				KnowledgeBaseId = document.KnowledgeBaseId,
				DocumentId = document.Id,
				Operation = AiOperationEnum.Embedding,
				Model = embedding.Model,
				PromptTokens = embedding.TotalTokens,
				LatencyMs = (int)Math.Min(stopwatch.ElapsedMilliseconds, int.MaxValue),
				IsEstimated = embedding.IsEstimated
			}, cancellationToken);

			// 单租户运行时 TenantId 为 null：payload 必须归一化成 Guid.Empty，否则 Qdrant 过滤恒不命中
			var tenantId = document.TenantId ?? Guid.Empty;
			var points = new List<VectorPoint>(chunks.Count);
			var entities = new List<DocumentChunk>(chunks.Count);

			for (var index = 0; index < chunks.Count; index++)
			{
				var chunk = chunks[index];
				var chunkId = _guidGenerator.Create();

				points.Add(new VectorPoint
				{
					Id = chunkId,
					Vector = vectors[index],
					SparseVector = SparseVectorBuilder.BuildDocument(
						string.IsNullOrWhiteSpace(chunk.HeadingPath)
							? chunk.Content
							: $"{chunk.HeadingPath}\n{chunk.Content}"),
					Payload = ChunkPayload.Create(
						tenantId,
						document.KnowledgeBaseId,
						document.Id,
						chunk.Index,
						chunk.Content,
						document.Title,
						chunk.HeadingPath,
						document.SourceType)
				});

				entities.Add(new DocumentChunk(chunkId, document.Id, chunk.Index, chunk.Content, chunk.HeadingPath, document.TenantId));
			}

			await _vectorStore.UpsertAsync(points, cancellationToken);
			await _chunkRepository.InsertManyAsync(entities, autoSave: true, cancellationToken: cancellationToken);

			document.MarkReady();
			await _documentRepository.UpdateAsync(document, autoSave: true, cancellationToken: cancellationToken);
			_logger.LogInformation("文档 {DocumentId} 摄入完成：{Count} 个切片", document.Id, chunks.Count);
		}

		/// <summary>
		/// 取本次切片参数：知识库设置了就用库设置，否则回落全局配置
		/// </summary>
		/// <param name="knowledgeBaseId">文档所属知识库</param>
		/// <param name="cancellationToken">取消令牌。</param>
		/// <returns>切片参数。</returns>
		private async Task<ChunkOptions> ResolveChunkOptionsAsync(Guid knowledgeBaseId, CancellationToken cancellationToken)
		{
			var knowledgeBase = await _knowledgeBaseRepository.FindAsync(knowledgeBaseId, cancellationToken: cancellationToken);
			var options = _ingestOptions.Value;

			return new ChunkOptions
			{
				SizeChars = knowledgeBase?.ChunkSizeChars ?? options.ChunkSizeChars,
				OverlapChars = knowledgeBase?.ChunkOverlapChars ?? options.ChunkOverlapChars
			};
		}
	}
}
