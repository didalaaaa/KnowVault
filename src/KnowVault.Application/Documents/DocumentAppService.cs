using KnowVault.Application.Contracts;
using KnowVault.Domain;
using KnowVault.Domain.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace KnowVault.Application
{
	/// <summary>文档应用服务：上传原件到对象存储 → 落库(Pending) → 发消息触发异步摄入。</summary>
	[Authorize]
	public class DocumentAppService : KnowVaultAppService, IDocumentAppService
	{
		private readonly IRepository<Document, Guid> _documentRepository;
		private readonly IRepository<DocumentChunk, Guid> _chunkRepository;
		private readonly IDocumentStorage _documentStorage;
		private readonly IVectorStore _vectorStore;
		private readonly IDocumentIngestPublisher _ingestPublisher;
		private readonly IOptions<DocumentUploadOptions> _uploadOptions;
		private readonly IUnitOfWorkManager _unitOfWorkManager;
		private readonly KnowledgeBaseAccessChecker _accessChecker;
		private readonly ILogger<DocumentAppService> _logger;

		/// <summary>构造函数。</summary>
		/// <param name="documentRepository">文档仓储。</param>
		/// <param name="chunkRepository">切片仓储。</param>
		/// <param name="documentStorage">对象存储。</param>
		/// <param name="vectorStore">向量库。</param>
		/// <param name="ingestPublisher">摄入消息发布者。</param>
		/// <param name="uploadOptions">上传约束（白名单与大小上限）。</param>
		/// <param name="unitOfWorkManager">工作单元管理器（用于「提交后再发消息」）。</param>
		/// <param name="accessChecker">知识库访问检查器。</param>
		/// <param name="logger">日志。</param>
		public DocumentAppService(
			IRepository<Document, Guid> documentRepository,
			IRepository<DocumentChunk, Guid> chunkRepository,
			IDocumentStorage documentStorage,
			IVectorStore vectorStore,
			IDocumentIngestPublisher ingestPublisher,
			IOptions<DocumentUploadOptions> uploadOptions,
			IUnitOfWorkManager unitOfWorkManager,
			KnowledgeBaseAccessChecker accessChecker,
			ILogger<DocumentAppService> logger)
		{
			_documentRepository = documentRepository;
			_chunkRepository = chunkRepository;
			_documentStorage = documentStorage;
			_vectorStore = vectorStore;
			_ingestPublisher = ingestPublisher;
			_uploadOptions = uploadOptions;
			_unitOfWorkManager = unitOfWorkManager;
			_accessChecker = accessChecker;
			_logger = logger;
		}

		/// <inheritdoc />
		/// <remarks>
		/// 用 <c>[RemoteService(IsEnabled = false)]</c> 把本方法移出 ABP 动态 API：
		/// DTO 里嵌套 <c>IRemoteStreamContent</c> 时，约定生成的 action 只接受 application/json，
		/// multipart 请求会被拒为 415；上传改由 <c>KnowVault.HttpApi.DocumentUploadController</c> 显式暴露。
		/// 注意：该特性必须标在**实现类的方法**上（标在接口上不生效，会与显式控制器产生路由冲突）。
		/// </remarks>
		[RemoteService(IsEnabled = false)]
		[Authorize(KnowVaultPermissions.Documents.Upload)]
		public async Task<DocumentDto> UploadAsync(UploadDocumentInput input, CancellationToken cancellationToken)
		{
			await _accessChecker.EnsureAsync(input.KnowledgeBaseId, KnowledgeBaseRoleEnum.Editor, cancellationToken);

			var fileName = input.File.FileName;
			if (string.IsNullOrWhiteSpace(fileName))
			{
				throw new UserFriendlyException("上传文件缺少文件名。");
			}

			var extension = Path.GetExtension(fileName).ToLowerInvariant();
			var options = _uploadOptions.Value;

			if (!options.AllowedExtensions.Contains(extension))
			{
				throw new UserFriendlyException($"不支持的文件类型：{extension}（支持 {options.AllowedExtensions.JoinAsString("、")}）");
			}

			// ContentLength 可能为 null（分块上传时未知），拿不到就不做上限校验
			var maxBytes = (long)options.MaxFileSizeMb * 1024 * 1024;
			long? contentLength = input.File.ContentLength;
			if (contentLength.HasValue && contentLength.Value > maxBytes)
			{
				throw new UserFriendlyException($"文件超过 {options.MaxFileSizeMb}MB 上限。");
			}

			var sourceType = DocumentSourceTypeResolver.FromExtension(extension) ?? throw new UserFriendlyException($"无法识别文件类型：{extension}");
			var title = string.IsNullOrWhiteSpace(input.Title) ? (Path.GetFileNameWithoutExtension(fileName) ?? "未命名文档") : input.Title;
			var document = new Document(GuidGenerator.Create(), input.KnowledgeBaseId, title, sourceType, CurrentTenant.Id);

			await _documentStorage.EnsureBucketAsync(cancellationToken);

			// 对象键带租户与知识库前缀，便于按租户清理与排查
			var objectKey = $"{(document.TenantId?.ToString() ?? "host")}/{input.KnowledgeBaseId}/{document.Id}{extension}";
			await using (var stream = input.File.GetStream())
			{
				await _documentStorage.PutAsync(objectKey, stream, input.File.ContentType ?? "application/octet-stream", cancellationToken);
			}

			document.ObjectStorageKey = objectKey;
			await _documentRepository.InsertAsync(document, autoSave: true, cancellationToken: cancellationToken);

			// 必须等当前 UoW 提交后再发消息：否则消费者可能先于提交去查这行文档，
			// 读到 null 就把消息白跑一趟（实测症状：文档永远停在 Pending，日志出现「文档 xxx 不存在，跳过处理」）。
			var unitOfWork = _unitOfWorkManager.Current;
			if (unitOfWork is not null)
			{
				unitOfWork.OnCompleted(async () => await _ingestPublisher.PublishAsync(document.Id, document.TenantId, cancellationToken));
			}
			else
			{
				await _ingestPublisher.PublishAsync(document.Id, document.TenantId, cancellationToken);
			}

			_logger.LogInformation("文档 {DocumentId} 已上传，将在事务提交后入队：{ObjectKey}", document.Id, objectKey);

			return MapToDto(document);
		}

		/// <inheritdoc />
		public async Task<DocumentDto> GetAsync(Guid id, CancellationToken cancellationToken)
		{
			var document = await _documentRepository.GetAsync(id, cancellationToken: cancellationToken);
			await _accessChecker.EnsureAsync(document.KnowledgeBaseId, KnowledgeBaseRoleEnum.Viewer, cancellationToken);
			return MapToDto(document);
		}

		/// <inheritdoc />
		public async Task<List<DocumentDto>> GetListAsync(Guid knowledgeBaseId, CancellationToken cancellationToken)
		{
			await _accessChecker.EnsureAsync(knowledgeBaseId, KnowledgeBaseRoleEnum.Viewer, cancellationToken);
			var documents = await _documentRepository.GetListAsync(x => x.KnowledgeBaseId == knowledgeBaseId, cancellationToken: cancellationToken);
			return documents.OrderByDescending(d => d.CreationTime).Select(MapToDto).ToList();
		}

		/// <inheritdoc />
		[Authorize(KnowVaultPermissions.Documents.Delete)]
		public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
		{
			var document = await _documentRepository.GetAsync(id, cancellationToken: cancellationToken);
			await _accessChecker.EnsureAsync(document.KnowledgeBaseId, KnowledgeBaseRoleEnum.Editor, cancellationToken);

			// 顺序很关键：向量 → 切片 → 文档 → 原件。
			// 前面任一步失败都保留文档，用户可重试；原件最后删，失败只留下孤儿对象（P4 清理）
			await _vectorStore.DeleteByDocumentAsync(
				document.TenantId ?? Guid.Empty,
				document.KnowledgeBaseId,
				document.Id,
				cancellationToken);

			await _chunkRepository.DeleteAsync(chunk => chunk.DocumentId == document.Id, autoSave: true, cancellationToken: cancellationToken);
			await _documentRepository.DeleteAsync(document, autoSave: true, cancellationToken: cancellationToken);

			if (!string.IsNullOrWhiteSpace(document.ObjectStorageKey))
			{
				try
				{
					await _documentStorage.DeleteAsync(document.ObjectStorageKey, cancellationToken);
				}
				catch (Exception ex) when (ex is not OperationCanceledException)
				{
					// 文档元数据已删，不能因为对象存储失败回滚；留给后续孤儿清理
					_logger.LogWarning(ex, "删除文档 {DocumentId} 的原件失败，可能留下孤儿对象：{ObjectKey}", document.Id, document.ObjectStorageKey);
				}
			}

			_logger.LogInformation("文档 {DocumentId} 已删除（含切片与向量）", id);
		}

		/// <inheritdoc />
		[Authorize(KnowVaultPermissions.Documents.Reprocess)]
		public async Task ReprocessAsync(Guid id, CancellationToken cancellationToken)
		{
			var document = await _documentRepository.GetAsync(id, cancellationToken: cancellationToken);
			await _accessChecker.EnsureAsync(document.KnowledgeBaseId, KnowledgeBaseRoleEnum.Editor, cancellationToken);

			if (document.Status == DocumentStatusEnum.Processing)
			{
				throw new UserFriendlyException("文档正在处理中，请稍后再试");
			}

			// 先清旧切片与向量，否则重新摄入会叠加出重复 chunk / 重复向量
			await _vectorStore.DeleteByDocumentAsync(
				document.TenantId ?? Guid.Empty,
				document.KnowledgeBaseId,
				document.Id,
				cancellationToken);

			await _chunkRepository.DeleteAsync(chunk => chunk.DocumentId == document.Id, autoSave: true, cancellationToken: cancellationToken);

			document.MarkPending();
			await _documentRepository.UpdateAsync(document, autoSave: true, cancellationToken: cancellationToken);

			await PublishAfterCommitAsync(document.Id, document.TenantId, cancellationToken);
			_logger.LogInformation("文档 {DocumentId} 已重新排队处理", document.Id);
		}

		private async Task PublishAfterCommitAsync(Guid documentId, Guid? tenantId, CancellationToken cancellationToken)
		{
			var unitOfWork = _unitOfWorkManager.Current;
			if (unitOfWork is not null)
			{
				unitOfWork.OnCompleted(async () => await _ingestPublisher.PublishAsync(documentId, tenantId, cancellationToken));
			}
			else
			{
				await _ingestPublisher.PublishAsync(documentId, tenantId, cancellationToken);
			}
		}

		private static DocumentDto MapToDto(Document document) => new()
		{
			Id = document.Id,
			KnowledgeBaseId = document.KnowledgeBaseId,
			Title = document.Title,
			SourceType = document.SourceType,
			Status = document.Status,
			ErrorMessage = document.ErrorMessage,
			IngestAttemptCount = document.IngestAttemptCount,
			ObjectStorageKey = document.ObjectStorageKey,
			CreationTime = document.CreationTime
		};
	}
}
