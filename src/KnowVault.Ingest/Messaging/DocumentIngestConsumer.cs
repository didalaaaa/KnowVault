using MassTransit;
using Microsoft.Extensions.Logging;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace KnowVault.Ingest
{
	/// <summary>
	/// 文档摄入消费者。
	/// 进程内运行（P1 决策 1），但保持宿主无关：自带 UoW 与租户切换，可直接平移到独立 Worker。
	/// 处理失败只记录到 Document（不重投），P2 再加重试与死信策略。
	/// </summary>
	public class DocumentIngestConsumer : IConsumer<DocumentIngestRequested>
	{
		private readonly IDocumentProcessor _processor;
		private readonly IUnitOfWorkManager _unitOfWorkManager;
		private readonly ICurrentTenant _currentTenant;
		private readonly ILogger<DocumentIngestConsumer> _logger;

		/// <summary>构造函数。</summary>
		/// <param name="processor">文档处理管线。</param>
		/// <param name="unitOfWorkManager">工作单元管理器。</param>
		/// <param name="currentTenant">当前租户。</param>
		/// <param name="logger">日志。</param>
		public DocumentIngestConsumer(
			IDocumentProcessor processor,
			IUnitOfWorkManager unitOfWorkManager,
			ICurrentTenant currentTenant,
			ILogger<DocumentIngestConsumer> logger)
		{
			_processor = processor;
			_unitOfWorkManager = unitOfWorkManager;
			_currentTenant = currentTenant;
			_logger = logger;
		}

		/// <inheritdoc />
		public async Task Consume(ConsumeContext<DocumentIngestRequested> context)
		{
			var message = context.Message;
			_logger.LogInformation("开始摄入文档 {DocumentId}（租户 {TenantId}）", message.DocumentId, message.TenantId);

			// consumer 不在 HTTP 管线里：没有 ABP 的工作单元，也没有租户上下文，必须自己开
			using (_currentTenant.Change(message.TenantId))
			using (var unitOfWork = _unitOfWorkManager.Begin(requiresNew: true))
			{
				await _processor.ProcessAsync(message.DocumentId, context.CancellationToken);
				await unitOfWork.CompleteAsync();
			}
		}
	}
}
