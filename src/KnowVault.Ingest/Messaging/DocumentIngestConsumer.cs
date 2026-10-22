using KnowVault.Domain;
using MassTransit;
using Microsoft.Extensions.Logging;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace KnowVault.Ingest
{
	/// <summary>
	/// 文档摄入消费者。
	/// 进程内运行（P1 决策 1），但保持宿主无关：自带 UoW 与租户切换，可直接平移到独立 Worker。
	/// 失败时在独立 UoW 中记录 Failed 后重新抛出，交给 MassTransit 重试；重试耗尽进 error 队列。
	/// </summary>
	public class DocumentIngestConsumer : IConsumer<DocumentIngestRequested>
	{
		private readonly IDocumentProcessor _processor;
		private readonly IRepository<Document, Guid> _documentRepository;
		private readonly IUnitOfWorkManager _unitOfWorkManager;
		private readonly ICurrentTenant _currentTenant;
		private readonly ILogger<DocumentIngestConsumer> _logger;

		/// <summary>构造函数。</summary>
		/// <param name="processor">文档处理管线。</param>
		/// <param name="documentRepository">文档仓储（失败落库用）。</param>
		/// <param name="unitOfWorkManager">工作单元管理器。</param>
		/// <param name="currentTenant">当前租户。</param>
		/// <param name="logger">日志。</param>
		public DocumentIngestConsumer(
			IDocumentProcessor processor,
			IRepository<Document, Guid> documentRepository,
			IUnitOfWorkManager unitOfWorkManager,
			ICurrentTenant currentTenant,
			ILogger<DocumentIngestConsumer> logger)
		{
			_processor = processor;
			_documentRepository = documentRepository;
			_unitOfWorkManager = unitOfWorkManager;
			_currentTenant = currentTenant;
			_logger = logger;
		}

		/// <inheritdoc />
		public async Task Consume(ConsumeContext<DocumentIngestRequested> context)
		{
			var message = context.Message;
			var cancellationToken = context.CancellationToken;
			_logger.LogInformation("开始摄入文档 {DocumentId}（租户 {TenantId}）", message.DocumentId, message.TenantId);

			try
			{
				// consumer 不在 HTTP 管线里：没有 ABP 的工作单元，也没有租户上下文，必须自己开
				using (_currentTenant.Change(message.TenantId))
				using (var unitOfWork = _unitOfWorkManager.Begin(requiresNew: true))
				{
					await _processor.ProcessAsync(message.DocumentId, cancellationToken);
					await unitOfWork.CompleteAsync(cancellationToken);
				}
			}
			catch (Exception exception) when (exception is not OperationCanceledException)
			{
				// 外层 UoW 会随异常回滚，Failed 状态必须单独提交，否则重试期间前端看不到失败原因
				await RecordFailureAsync(message, exception, cancellationToken);
				throw;
			}
		}

		private async Task RecordFailureAsync(DocumentIngestRequested message, Exception exception, CancellationToken cancellationToken)
		{
			_logger.LogError(exception, "文档 {DocumentId} 摄入失败，将按重试策略重新投递", message.DocumentId);

			try
			{
				using (_currentTenant.Change(message.TenantId))
				using (var unitOfWork = _unitOfWorkManager.Begin(requiresNew: true))
				{
					var document = await _documentRepository.FindAsync(message.DocumentId, cancellationToken: cancellationToken);
					if (document is not null)
					{
						document.MarkFailed(exception.Message);
						await _documentRepository.UpdateAsync(document, autoSave: true, cancellationToken: cancellationToken);
					}

					await unitOfWork.CompleteAsync(cancellationToken);
				}
			}
			catch (Exception recordException)
			{
				// 记录失败状态本身再失败时，不能吞掉原始异常，否则重试原因会被掩盖
				_logger.LogError(recordException, "记录文档 {DocumentId} 的失败状态失败", message.DocumentId);
			}
		}
	}
}
