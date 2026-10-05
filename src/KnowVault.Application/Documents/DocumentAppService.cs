using KnowVault.Application.Contracts;
using KnowVault.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;

namespace KnowVault.Application
{
	/// <summary>文档应用服务：上传原件到对象存储 → 落库(Pending) → 发消息触发异步摄入。</summary>
	[Authorize]
	public class DocumentAppService : KnowVaultAppService, IDocumentAppService
	{
		private readonly IRepository<Document, Guid> _documentRepository;
		private readonly IDocumentStorage _documentStorage;
		private readonly IDocumentIngestPublisher _ingestPublisher;
		private readonly IOptions<DocumentUploadOptions> _uploadOptions;
		private readonly ILogger<DocumentAppService> _logger;

		/// <summary>构造函数。</summary>
		/// <param name="documentRepository">文档仓储。</param>
		/// <param name="documentStorage">对象存储。</param>
		/// <param name="ingestPublisher">摄入消息发布者。</param>
		/// <param name="uploadOptions">上传约束（白名单与大小上限）。</param>
		/// <param name="logger">日志。</param>
		public DocumentAppService(
			IRepository<Document, Guid> documentRepository,
			IDocumentStorage documentStorage,
			IDocumentIngestPublisher ingestPublisher,
			IOptions<DocumentUploadOptions> uploadOptions,
			ILogger<DocumentAppService> logger)
		{
			_documentRepository = documentRepository;
			_documentStorage = documentStorage;
			_ingestPublisher = ingestPublisher;
			_uploadOptions = uploadOptions;
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
		public async Task<DocumentDto> UploadAsync(UploadDocumentInput input)
		{
			var fileName = input.File.FileName;
			if (string.IsNullOrWhiteSpace(fileName))
			{
				throw new UserFriendlyException("上传文件缺少文件名。");
			}

			var extension = Path.GetExtension(fileName).ToLowerInvariant();
			var options = _uploadOptions.Value;

			if (!options.AllowedExtensions.Contains(extension))
			{
				throw new UserFriendlyException(
					$"不支持的文件类型：{extension}（支持 {string.Join("、", options.AllowedExtensions)}）");
			}

			// ContentLength 可能为 null（分块上传时未知），拿不到就不做上限校验
			var maxBytes = (long)options.MaxFileSizeMb * 1024 * 1024;
			long? contentLength = input.File.ContentLength;
			if (contentLength.HasValue && contentLength.Value > maxBytes)
			{
				throw new UserFriendlyException($"文件超过 {options.MaxFileSizeMb}MB 上限。");
			}

			var sourceType = DocumentSourceTypeResolver.FromExtension(extension)
							 ?? throw new UserFriendlyException($"无法识别文件类型：{extension}");

			var title = string.IsNullOrWhiteSpace(input.Title)
				? Path.GetFileNameWithoutExtension(fileName) ?? "未命名文档"
				: input.Title!;

			var document = new Document(
				GuidGenerator.Create(),
				input.KnowledgeBaseId,
				title,
				sourceType,
				CurrentTenant.Id);

			await _documentStorage.EnsureBucketAsync();

			// 对象键带租户与知识库前缀，便于按租户清理与排查
			var objectKey = $"{(document.TenantId?.ToString() ?? "host")}/{input.KnowledgeBaseId}/{document.Id}{extension}";
			await using (var stream = input.File.GetStream())
			{
				await _documentStorage.PutAsync(objectKey, stream, input.File.ContentType ?? "application/octet-stream");
			}

			document.ObjectStorageKey = objectKey;
			await _documentRepository.InsertAsync(document, autoSave: true);

			await _ingestPublisher.PublishAsync(document.Id, document.TenantId);
			_logger.LogInformation("文档 {DocumentId} 已上传并入队：{ObjectKey}", document.Id, objectKey);

			return MapToDto(document);
		}

		/// <inheritdoc />
		public async Task<DocumentDto> GetAsync(Guid id)
		{
			var document = await _documentRepository.GetAsync(id);
			return MapToDto(document);
		}

		/// <inheritdoc />
		public async Task<List<DocumentDto>> GetListAsync(Guid knowledgeBaseId)
		{
			var documents = await _documentRepository.GetListAsync(x => x.KnowledgeBaseId == knowledgeBaseId);
			return documents.OrderByDescending(d => d.CreationTime).Select(MapToDto).ToList();
		}

		private static DocumentDto MapToDto(Document document) => new()
		{
			Id = document.Id,
			KnowledgeBaseId = document.KnowledgeBaseId,
			Title = document.Title,
			SourceType = document.SourceType,
			Status = document.Status,
			ErrorMessage = document.ErrorMessage,
			ObjectStorageKey = document.ObjectStorageKey,
			CreationTime = document.CreationTime
		};
	}
}
