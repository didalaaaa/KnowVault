using KnowVault.Domain;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

namespace KnowVault.Ingest
{
	/// <summary>
	/// 文档处理管线。失败一律记录到 <see cref="Document.Status"/>=Failed 并结束（P1 不做自动重试）。
	/// </summary>
	public class DocumentProcessor : IDocumentProcessor, ITransientDependency
	{
		private readonly IRepository<Document, Guid> _documentRepository;
		private readonly IRepository<DocumentChunk, Guid> _chunkRepository;
		private readonly IDocumentStorage _storage;
		private readonly DocumentParserRegistry _parserRegistry;
		private readonly IDocumentChunker _chunker;
		private readonly IEmbeddingService _embeddingService;
		private readonly IVectorStore _vectorStore;
		private readonly IGuidGenerator _guidGenerator;
		private readonly ILogger<DocumentProcessor> _logger;

		/// <summary>构造函数。</summary>
		/// <param name="documentRepository">文档仓储。</param>
		/// <param name="chunkRepository">切片仓储。</param>
		/// <param name="storage">对象存储。</param>
		/// <param name="parserRegistry">解析器注册表。</param>
		/// <param name="chunker">切片器。</param>
		/// <param name="embeddingService">向量化服务。</param>
		/// <param name="vectorStore">向量库。</param>
		/// <param name="guidGenerator">GUID 生成器（ABP 约定）。</param>
		/// <param name="logger">日志。</param>
		public DocumentProcessor(
			IRepository<Document, Guid> documentRepository,
			IRepository<DocumentChunk, Guid> chunkRepository,
			IDocumentStorage storage,
			DocumentParserRegistry parserRegistry,
			IDocumentChunker chunker,
			IEmbeddingService embeddingService,
			IVectorStore vectorStore,
			IGuidGenerator guidGenerator,
			ILogger<DocumentProcessor> logger)
		{
			_documentRepository = documentRepository;
			_chunkRepository = chunkRepository;
			_storage = storage;
			_parserRegistry = parserRegistry;
			_chunker = chunker;
			_embeddingService = embeddingService;
			_vectorStore = vectorStore;
			_guidGenerator = guidGenerator;
			_logger = logger;
		}

		/// <inheritdoc />
		public async Task ProcessAsync(Guid documentId, CancellationToken cancellationToken = default)
		{
			var document = await _documentRepository.FindAsync(documentId, cancellationToken: cancellationToken);
			if (document is null)
			{
				_logger.LogWarning("文档 {DocumentId} 不存在，跳过处理", documentId);
				return;
			}

			try
			{
				await ProcessInternalAsync(document, cancellationToken);
			}
			catch (Exception exception)
			{
				// P1 策略：把失败固化到文档上，事务照常提交，消息常 ACK（不重投）
				_logger.LogError(exception, "文档 {DocumentId} 摄入失败", documentId);
				document.MarkFailed(exception.Message);
				await _documentRepository.UpdateAsync(document, autoSave: true, cancellationToken: cancellationToken);
			}
		}

		private async Task ProcessInternalAsync(Document document, CancellationToken cancellationToken)
		{
			if (string.IsNullOrWhiteSpace(document.ObjectStorageKey))
			{
				throw new InvalidOperationException("文档缺少对象存储键，无法解析。");
			}

			document.MarkProcessing();
			await _documentRepository.UpdateAsync(document, autoSave: true, cancellationToken: cancellationToken);

			var extension = Path.GetExtension(document.ObjectStorageKey).ToLowerInvariant();
			var parser = _parserRegistry.Resolve(extension);

			ParsedDocument parsed;
			await using (var stream = await _storage.GetAsync(document.ObjectStorageKey!, cancellationToken))
			{
				parsed = parser.Parse(stream, document.ObjectStorageKey!);
			}

			var chunks = _chunker.Chunk(parsed);
			if (chunks.Count == 0)
			{
				throw new InvalidOperationException("未从文档中解析出任何文本内容。");
			}

			var vectors = await _embeddingService.EmbedAsync(
				chunks.Select(c => c.Content).ToList(), cancellationToken);

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

				entities.Add(new DocumentChunk(chunkId, document.Id, chunk.Index, chunk.Content, document.TenantId));
			}

			await _vectorStore.UpsertAsync(points, cancellationToken);
			await _chunkRepository.InsertManyAsync(entities, autoSave: true, cancellationToken: cancellationToken);

			document.MarkReady();
			await _documentRepository.UpdateAsync(document, autoSave: true, cancellationToken: cancellationToken);
			_logger.LogInformation("文档 {DocumentId} 摄入完成：{Count} 个切片", document.Id, chunks.Count);
		}
	}
}
