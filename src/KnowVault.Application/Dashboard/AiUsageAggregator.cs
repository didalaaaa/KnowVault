using KnowVault.Application.Contracts;
using KnowVault.Domain;

namespace KnowVault.Application
{
	/// <summary>
	/// 把最近 7 天的用量记录聚合为仪表盘用量区数据
	/// </summary>
	public static class AiUsageAggregator
	{
		private const string MultiKnowledgeBaseName = "多库/全局";
		private const string DeletedKnowledgeBaseName = "已删除知识库";

		/// <summary>
		/// 聚合用量记录：今日与 7 天总计、平均每次提问、每日趋势与按库汇总
		/// </summary>
		/// <param name="records">已筛选的最近 7 天用量记录</param>
		/// <param name="knowledgeBaseNames">知识库 ID 到名称的映射</param>
		/// <param name="today">今天（本地日期）</param>
		/// <returns>仪表盘用量汇总</returns>
		public static DashboardUsageDto Aggregate(
			IReadOnlyList<AiUsageRecord> records,
			IReadOnlyDictionary<Guid, string> knowledgeBaseNames,
			DateTime today)
		{
			var todayDate = today.Date;
			var trendStart = todayDate.AddDays(-6);

			var questionRecords = records.Where(record => record.AskId.HasValue).ToList();
			var askIdCount = questionRecords.Select(record => record.AskId!.Value).Distinct().Count();
			var questionTokens = questionRecords.Sum(record => record.TotalTokens);
			var todayRecords = records.Where(record => record.CreationTime.Date == todayDate).ToList();

			return new DashboardUsageDto
			{
				TodayTokens = todayRecords.Sum(record => record.TotalTokens),
				TodayEstimatedCostCny = todayRecords.Sum(record => record.EstimatedCostCny),
				Last7DayTokens = records.Sum(record => record.TotalTokens),
				AverageTokensPerQuestion = askIdCount == 0 ? 0 : (int)Math.Round(questionTokens / (double)askIdCount),
				HasEstimated = records.Any(record => record.IsEstimated),
				Last7DayTrend = [.. Enumerable.Range(0, 7).Select(offset =>
				{
					var date = trendStart.AddDays(offset);
					var dayRecords = records.Where(record => record.CreationTime.Date == date).ToList();
					return new DashboardUsageTrendPointDto
					{
						Date = date,
						PromptTokens = dayRecords.Sum(record => record.PromptTokens),
						CompletionTokens = dayRecords.Sum(record => record.CompletionTokens),
						TotalTokens = dayRecords.Sum(record => record.TotalTokens),
						EstimatedCostCny = dayRecords.Sum(record => record.EstimatedCostCny)
					};
				})],
				ByKnowledgeBase = [.. records
					.GroupBy(record => record.KnowledgeBaseId)
					.Select(group => new DashboardKnowledgeBaseUsageDto
					{
						KnowledgeBaseId = group.Key,
						KnowledgeBaseName = ResolveName(group.Key, knowledgeBaseNames),
						QuestionCount = group
							.Where(record => record.AskId.HasValue)
							.Select(record => record.AskId!.Value)
							.Distinct()
							.Count(),
						TotalTokens = group.Sum(record => record.TotalTokens),
						EstimatedCostCny = group.Sum(record => record.EstimatedCostCny)
					})
					.OrderByDescending(item => item.TotalTokens)
					.ThenBy(item => item.KnowledgeBaseName)]
			};
		}

		private static string ResolveName(Guid? knowledgeBaseId, IReadOnlyDictionary<Guid, string> knowledgeBaseNames)
		{
			if (knowledgeBaseId is not { } id) return MultiKnowledgeBaseName;

			return knowledgeBaseNames.TryGetValue(id, out var name) ? name : DeletedKnowledgeBaseName;
		}
	}
}
