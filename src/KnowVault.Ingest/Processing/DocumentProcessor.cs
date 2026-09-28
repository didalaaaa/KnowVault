using KnowVault.Domain;
using KnowVault.Domain.Shared;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace KnowVault.Ingest
{

	/// <summary>
	/// P0 骨架实现：仅统计待处理文档。真实解析/切片/向量化（P1）在此实现。
	/// </summary>
	public class DocumentProcessor : IDocumentProcessor, ITransientDependency
	{
		private readonly IRepository<Document, Guid> _documentRepository;
		private readonly ILogger<DocumentProcessor> _logger;

		public DocumentProcessor(
			IRepository<Document, Guid> documentRepository,
			ILogger<DocumentProcessor> logger)
		{
			_documentRepository = documentRepository;
			_logger = logger;
		}

		public async Task ProcessPendingDocumentsAsync(CancellationToken cancellationToken = default)
		{
			var pending = await _documentRepository.CountAsync(
				x => x.Status == DocumentStatusEnum.Pending,
				cancellationToken: cancellationToken);

			_logger.LogInformation("Ingest 心跳：待处理文档 {Pending} 个（P1 将经 RabbitMQ 消费队列完成解析/切片/向量化）", pending);
		}
	}

}
