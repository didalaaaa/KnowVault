namespace KnowVault.Application.Contracts
{
	/// <summary>
	/// 仪表盘每日用量趋势点
	/// </summary>
	public class DashboardUsageTrendPointDto
	{
		/// <summary>
		/// 日期（服务器本地时区，零点）
		/// </summary>
		public DateTime Date { get; set; }

		/// <summary>
		/// 当天输入 token 数
		/// </summary>
		public int PromptTokens { get; set; }

		/// <summary>
		/// 当天输出 token 数
		/// </summary>
		public int CompletionTokens { get; set; }

		/// <summary>
		/// 当天 token 总量
		/// </summary>
		public int TotalTokens { get; set; }

		/// <summary>
		/// 当天估算费用（人民币元）
		/// </summary>
		public decimal EstimatedCostCny { get; set; }
	}
}
