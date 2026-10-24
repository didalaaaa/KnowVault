namespace KnowVault.Application.Contracts
{
	/// <summary>
	/// 仪表盘趋势点
	/// </summary>
	public class DashboardTrendPointDto
	{
		/// <summary>
		/// 日期（服务器本地时区，零点）
		/// </summary>
		public DateTime Date { get; set; }

		/// <summary>
		/// 当天提问数
		/// </summary>
		public int Count { get; set; }
	}
}
