namespace KnowVault.Application.Contracts
{
	/// <summary>
	/// 仪表盘按知识库的用量汇总
	/// </summary>
	public class DashboardKnowledgeBaseUsageDto
	{
		/// <summary>
		/// 知识库 ID；多库/全局问答为空
		/// </summary>
		public Guid? KnowledgeBaseId { get; set; }

		/// <summary>
		/// 知识库名称；已删除的库显示占位名
		/// </summary>
		public string KnowledgeBaseName { get; set; } = string.Empty;

		/// <summary>
		/// 该库下的提问次数（不同 AskId 数）
		/// </summary>
		public int QuestionCount { get; set; }

		/// <summary>
		/// 该库的 token 总量（含问答与摄取）
		/// </summary>
		public int TotalTokens { get; set; }

		/// <summary>
		/// 该库的估算费用（人民币元）
		/// </summary>
		public decimal EstimatedCostCny { get; set; }
	}
}
