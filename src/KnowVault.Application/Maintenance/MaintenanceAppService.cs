using KnowVault.Application.Contracts;
using KnowVault.Domain;
using KnowVault.Domain.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Volo.Abp.Domain.Repositories;

namespace KnowVault.Application
{
	/// <summary>
	/// 运维应用服务：重建向量库 schema 并重新处理文档
	/// </summary>
	[Authorize(KnowVaultPermissions.KnowledgeBases.Manage)]
	public class MaintenanceAppService : KnowVaultAppService, IMaintenanceAppService
	{
		private readonly IVectorStore _vectorStore;
		private readonly IRepository<Document, Guid> _documentRepository;
		private readonly IDocumentAppService _documentAppService;
		private readonly ILogger<MaintenanceAppService> _logger;

		/// <summary>
		/// 构造函数
		/// </summary>
		/// <param name="vectorStore">向量库</param>
		/// <param name="documentRepository">文档仓储</param>
		/// <param name="documentAppService">文档应用服务（复用重新处理流程）</param>
		/// <param name="logger">日志</param>
		public MaintenanceAppService(
			IVectorStore vectorStore,
			IRepository<Document, Guid> documentRepository,
			IDocumentAppService documentAppService,
			ILogger<MaintenanceAppService> logger)
		{
			_vectorStore = vectorStore;
			_documentRepository = documentRepository;
			_documentAppService = documentAppService;
			_logger = logger;
		}

		/// <inheritdoc />
		public async Task<RebuildVectorIndexResultDto> RebuildVectorIndexAsync(CancellationToken cancellationToken)
		{
			await _vectorStore.RecreateCollectionAsync(cancellationToken);

			var documents = await _documentRepository.GetListAsync(
				document => document.Status == DocumentStatusEnum.Ready || document.Status == DocumentStatusEnum.Processing,
				cancellationToken: cancellationToken);

			var skippedDocumentIds = new List<Guid>();
			var requeuedCount = 0;

			foreach (var document in documents)
			{
				if (document.Status == DocumentStatusEnum.Processing)
				{
					skippedDocumentIds.Add(document.Id);
					continue;
				}

				await _documentAppService.ReprocessAsync(document.Id, cancellationToken);
				requeuedCount++;
			}

			_logger.LogInformation(
				"向量索引重建完成：重新排队 {RequeuedCount} 个文档，跳过 {SkippedCount} 个处理中的文档",
				requeuedCount,
				skippedDocumentIds.Count);

			return new RebuildVectorIndexResultDto
			{
				RequeuedCount = requeuedCount,
				SkippedCount = skippedDocumentIds.Count,
				SkippedDocumentIds = skippedDocumentIds
			};
		}
	}
}
