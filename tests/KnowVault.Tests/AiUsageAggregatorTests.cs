using KnowVault.Application;
using KnowVault.Domain;
using KnowVault.Domain.Shared;
using Xunit;

namespace KnowVault.Tests
{
	/// <summary>
	/// 仪表盘用量聚合的纯函数测试
	/// </summary>
	public class AiUsageAggregatorTests
	{
		private static readonly DateTime Today = new(2026, 10, 27);

		/// <summary>
		/// 今日与 7 天总量、平均每次提问与趋势补零
		/// </summary>
		[Fact]
		public void Aggregate_sums_today_and_last_7_days()
		{
			var knowledgeBaseId = Guid.NewGuid();
			var askId = Guid.NewGuid();
			var records = new List<AiUsageRecord>
			{
				Record(knowledgeBaseId, null, 100, 0, 0.01m, Today.AddHours(9)),
				Record(knowledgeBaseId, askId, 50, 30, 0.02m, Today.AddHours(10)),
				Record(knowledgeBaseId, Guid.NewGuid(), 40, 0, 0.005m, Today.AddDays(-1).AddHours(8))
			};

			var usage = AiUsageAggregator.Aggregate(records, Names(knowledgeBaseId, "产品库"), Today);

			Assert.Equal(180, usage.TodayTokens);
			Assert.Equal(0.03m, usage.TodayEstimatedCostCny);
			Assert.Equal(220, usage.Last7DayTokens);
			Assert.Equal(60, usage.AverageTokensPerQuestion);
			Assert.False(usage.HasEstimated);
			Assert.Equal(7, usage.Last7DayTrend.Count);
			Assert.Equal(Today.AddDays(-6), usage.Last7DayTrend[0].Date);
			Assert.Equal(0, usage.Last7DayTrend[0].TotalTokens);
			Assert.Equal(40, usage.Last7DayTrend[5].TotalTokens);
			Assert.Equal(180, usage.Last7DayTrend[6].TotalTokens);
			Assert.Equal(150, usage.Last7DayTrend[6].PromptTokens);
			Assert.Equal(30, usage.Last7DayTrend[6].CompletionTokens);
		}

		/// <summary>
		/// 按库汇总区分摄取与提问，多库与已删除库使用占位名
		/// </summary>
		[Fact]
		public void Aggregate_groups_by_knowledge_base_and_marks_placeholders()
		{
			var productKnowledgeBaseId = Guid.NewGuid();
			var deletedKnowledgeBaseId = Guid.NewGuid();
			var askId = Guid.NewGuid();
			var records = new List<AiUsageRecord>
			{
				Record(productKnowledgeBaseId, askId, 100, 0, 1m, Today),
				Record(productKnowledgeBaseId, askId, 50, 0, 0.5m, Today),
				Record(productKnowledgeBaseId, null, 30, 0, 0.3m, Today),
				Record(deletedKnowledgeBaseId, Guid.NewGuid(), 20, 0, 0.2m, Today),
				Record(null, Guid.NewGuid(), 10, 0, 0.1m, Today),
				Record(null, Guid.NewGuid(), 5, 0, 0.05m, Today)
			};

			var usage = AiUsageAggregator.Aggregate(records, Names(productKnowledgeBaseId, "产品库"), Today);

			var product = usage.ByKnowledgeBase.Single(item => item.KnowledgeBaseId == productKnowledgeBaseId);
			Assert.Equal("产品库", product.KnowledgeBaseName);
			Assert.Equal(1, product.QuestionCount);
			Assert.Equal(180, product.TotalTokens);

			var deleted = usage.ByKnowledgeBase.Single(item => item.KnowledgeBaseId == deletedKnowledgeBaseId);
			Assert.Equal("已删除知识库", deleted.KnowledgeBaseName);
			Assert.Equal(1, deleted.QuestionCount);

			var global = usage.ByKnowledgeBase.Single(item => item.KnowledgeBaseId is null);
			Assert.Equal("多库/全局", global.KnowledgeBaseName);
			Assert.Equal(2, global.QuestionCount);
			Assert.Equal(15, global.TotalTokens);

			Assert.Equal(46, usage.AverageTokensPerQuestion);
		}

		/// <summary>
		/// 空记录返回零值，趋势仍补齐 7 天
		/// </summary>
		[Fact]
		public void Aggregate_returns_zeros_for_empty_records()
		{
			var usage = AiUsageAggregator.Aggregate([], new Dictionary<Guid, string>(), Today);

			Assert.Equal(0, usage.TodayTokens);
			Assert.Equal(0m, usage.TodayEstimatedCostCny);
			Assert.Equal(0, usage.Last7DayTokens);
			Assert.Equal(0, usage.AverageTokensPerQuestion);
			Assert.False(usage.HasEstimated);
			Assert.Equal(7, usage.Last7DayTrend.Count);
			Assert.All(usage.Last7DayTrend, point => Assert.Equal(0, point.TotalTokens));
			Assert.Empty(usage.ByKnowledgeBase);
		}

		/// <summary>
		/// 任意一条记录来自本地估算时标记整段用量含估算值
		/// </summary>
		[Fact]
		public void Aggregate_flags_estimated_usage()
		{
			var record = Record(Guid.NewGuid(), Guid.NewGuid(), 10, 5, 0.001m, Today);
			record.IsEstimated = true;

			var usage = AiUsageAggregator.Aggregate([record], new Dictionary<Guid, string>(), Today);

			Assert.True(usage.HasEstimated);
		}

		private static AiUsageRecord Record(
			Guid? knowledgeBaseId,
			Guid? askId,
			int promptTokens,
			int completionTokens,
			decimal cost,
			DateTime creationTime)
		{
			return new AiUsageRecord(Guid.NewGuid())
			{
				KnowledgeBaseId = knowledgeBaseId,
				AskId = askId,
				Operation = askId.HasValue ? AiOperationEnum.Chat : AiOperationEnum.Embedding,
				Model = "fake-model",
				PromptTokens = promptTokens,
				CompletionTokens = completionTokens,
				TotalTokens = promptTokens + completionTokens,
				EstimatedCostCny = cost,
				CreationTime = creationTime
			};
		}

		private static IReadOnlyDictionary<Guid, string> Names(Guid id, string name) => new Dictionary<Guid, string> { [id] = name };
	}
}
