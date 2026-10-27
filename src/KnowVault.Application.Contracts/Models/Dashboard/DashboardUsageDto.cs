namespace KnowVault.Application.Contracts
{
	/// <summary>
	/// 仪表盘用量汇总：最近 7 天 token、估算成本与按库分布
	/// </summary>
	public class DashboardUsageDto
	{
		/// <summary>
		/// 今天的 token 总量
		/// </summary>
		public int TodayTokens { get; set; }

		/// <summary>
		/// 今天的估算费用（人民币元）
		/// </summary>
		public decimal TodayEstimatedCostCny { get; set; }

		/// <summary>
		/// 最近 7 天的 token 总量
		/// </summary>
		public int Last7DayTokens { get; set; }

		/// <summary>
		/// 平均每次提问的 token 数；没有问答时为 0
		/// </summary>
		public int AverageTokensPerQuestion { get; set; }

		/// <summary>
		/// 周期内是否存在本地估算的 token
		/// </summary>
		public bool HasEstimated { get; set; }

		/// <summary>
		/// 最近 7 天每天的用量（含今天，按日期升序，空日期补 0）
		/// </summary>
		public IReadOnlyList<DashboardUsageTrendPointDto> Last7DayTrend { get; set; } = [];

		/// <summary>
		/// 按知识库汇总；多库/全局问答单列一行
		/// </summary>
		public IReadOnlyList<DashboardKnowledgeBaseUsageDto> ByKnowledgeBase { get; set; } = [];
	}
}
