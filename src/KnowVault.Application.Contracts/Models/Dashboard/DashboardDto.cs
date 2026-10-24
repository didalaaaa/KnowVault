namespace KnowVault.Application.Contracts
{
	/// <summary>
	/// 仪表盘汇总：仅统计当前用户可见的知识库与其本人会话
	/// </summary>
	public class DashboardDto
	{
		/// <summary>
		/// 当前用户可见的知识库数
		/// </summary>
		public int KnowledgeBaseCount { get; set; }

		/// <summary>
		/// 上述知识库下的文档总数
		/// </summary>
		public int DocumentCount { get; set; }

		/// <summary>
		/// 已就绪文档数
		/// </summary>
		public int ReadyDocumentCount { get; set; }

		/// <summary>
		/// 处理失败文档数
		/// </summary>
		public int FailedDocumentCount { get; set; }

		/// <summary>
		/// 当前用户创建的会话数
		/// </summary>
		public int ConversationCount { get; set; }

		/// <summary>
		/// 这些会话里的用户提问总数
		/// </summary>
		public int QuestionCount { get; set; }

		/// <summary>
		/// 今天（服务器本地日期）的提问数
		/// </summary>
		public int TodayQuestionCount { get; set; }

		/// <summary>
		/// 最近 7 天助手回答的平均耗时（毫秒）；没有样本时为 0
		/// </summary>
		public double AverageLatencyMs { get; set; }

		/// <summary>
		/// 最近 7 天每天提问数（含今天，按日期升序）
		/// </summary>
		public IReadOnlyList<DashboardTrendPointDto> Last7DayQuestions { get; set; } = [];
	}
}
