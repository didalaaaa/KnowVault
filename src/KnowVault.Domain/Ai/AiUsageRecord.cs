using KnowVault.Domain.Shared;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowVault.Domain
{
	/// <summary>
	/// 一次 AI 调用的用量与费用记录
	/// </summary>
	public class AiUsageRecord : CreationAuditedAggregateRoot<Guid>, IMultiTenant
	{
		/// <summary>
		/// 租户 ID，单租户运行时为空
		/// </summary>
		public Guid? TenantId { get; set; }

		/// <summary>
		/// 关联知识库；多库/全局问答为空
		/// </summary>
		public Guid? KnowledgeBaseId { get; set; }

		/// <summary>
		/// 关联会话；问答类调用有值
		/// </summary>
		public Guid? ConversationId { get; set; }

		/// <summary>
		/// 同一次问答的关联 ID；摄取调用为空
		/// </summary>
		public Guid? AskId { get; set; }

		/// <summary>
		/// 关联文档；摄取向量化时有值
		/// </summary>
		public Guid? DocumentId { get; set; }

		/// <summary>
		/// AI 调用类型
		/// </summary>
		public AiOperationEnum Operation { get; set; }

		/// <summary>
		/// 实际使用的模型名
		/// </summary>
		public string Model { get; set; } = string.Empty;

		/// <summary>
		/// 输入 token 数
		/// </summary>
		public int PromptTokens { get; set; }

		/// <summary>
		/// 输出 token 数；embedding / rerank 为 0
		/// </summary>
		public int CompletionTokens { get; set; }

		/// <summary>
		/// 输入与输出 token 之和
		/// </summary>
		public int TotalTokens { get; set; }

		/// <summary>
		/// 该次调用耗时（毫秒）
		/// </summary>
		public int LatencyMs { get; set; }

		/// <summary>
		/// 是否由本地估算 token
		/// </summary>
		public bool IsEstimated { get; set; }

		/// <summary>
		/// 写入时按当时价格计算的估算费用（人民币元）
		/// </summary>
		public decimal EstimatedCostCny { get; set; }

		protected AiUsageRecord()
		{
		}

		/// <summary>
		/// 构造函数
		/// </summary>
		/// <param name="id">记录 ID</param>
		public AiUsageRecord(Guid id) : base(id)
		{
		}
	}
}
