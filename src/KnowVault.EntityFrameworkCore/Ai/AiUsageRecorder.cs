using KnowVault.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Uow;

namespace KnowVault.EntityFrameworkCore
{
	/// <summary>
	/// AI 用量记录器：独立 UoW 落库，写入失败只记 warning，不影响主流程
	/// </summary>
	public class AiUsageRecorder : IAiUsageRecorder, ITransientDependency
	{
		private readonly IRepository<AiUsageRecord, Guid> _repository;
		private readonly IUnitOfWorkManager _unitOfWorkManager;
		private readonly IGuidGenerator _guidGenerator;
		private readonly IOptions<AiPricingOptions> _pricing;
		private readonly ILogger<AiUsageRecorder> _logger;

		/// <summary>
		/// 构造函数
		/// </summary>
		/// <param name="repository">用量记录仓储</param>
		/// <param name="unitOfWorkManager">工作单元管理器</param>
		/// <param name="guidGenerator">GUID 生成器</param>
		/// <param name="pricing">计价配置</param>
		/// <param name="logger">日志</param>
		public AiUsageRecorder(
			IRepository<AiUsageRecord, Guid> repository,
			IUnitOfWorkManager unitOfWorkManager,
			IGuidGenerator guidGenerator,
			IOptions<AiPricingOptions> pricing,
			ILogger<AiUsageRecorder> logger)
		{
			_repository = repository;
			_unitOfWorkManager = unitOfWorkManager;
			_guidGenerator = guidGenerator;
			_pricing = pricing;
			_logger = logger;
		}

		/// <inheritdoc />
		public async Task RecordAsync(AiUsageEntry entry, CancellationToken cancellationToken = default)
		{
			try
			{
				var record = new AiUsageRecord(_guidGenerator.Create())
				{
					KnowledgeBaseId = entry.KnowledgeBaseId,
					ConversationId = entry.ConversationId,
					AskId = entry.AskId,
					DocumentId = entry.DocumentId,
					Operation = entry.Operation,
					Model = entry.Model,
					PromptTokens = entry.PromptTokens,
					CompletionTokens = entry.CompletionTokens,
					TotalTokens = entry.PromptTokens + entry.CompletionTokens,
					LatencyMs = entry.LatencyMs,
					IsEstimated = entry.IsEstimated,
					EstimatedCostCny = AiUsageCostCalculator.Calculate(
						entry.Operation,
						entry.PromptTokens,
						entry.CompletionTokens,
						_pricing.Value)
				};

				using var unitOfWork = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: false);
				await _repository.InsertAsync(record, autoSave: true, cancellationToken: cancellationToken);
				await unitOfWork.CompleteAsync(cancellationToken);
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				// 用量是旁路数据，写失败不能把问答或文档处理带崩
				_logger.LogWarning(ex, "AI 用量记录写入失败：{Operation} {Model}", entry.Operation, entry.Model);
			}
		}
	}
}
