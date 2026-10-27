using System.Diagnostics;
using KnowVault.Ai;
using KnowVault.Domain;
using KnowVault.Domain.Shared;
using Microsoft.Extensions.Options;

namespace KnowVault.Eval
{
	/// <summary>
	/// 顺序执行三种检索模式并汇总指标
	/// </summary>
	public class EvalRunner
	{
		private const int AnswerMaxTokens = 800;

		private static readonly string[] RefusalMarkers =
		[
			"资料不足", "无法回答", "没有找到", "未找到", "不知道", "无法确定", "没有相关", "未提供"
		];

		private readonly IRagService _ragService;
		private readonly ILlmChatService _chatService;
		private readonly CollectingUsageRecorder _usageRecorder;
		private readonly AiPricingOptions _pricing;
		private readonly EvalOptions _options;
		private readonly GoldenSet _goldenSet;
		private readonly AnchorIndex _anchorIndex;

		/// <summary>
		/// 构造函数
		/// </summary>
		/// <param name="ragService">RAG 检索入口</param>
		/// <param name="chatService">生成模型</param>
		/// <param name="usageRecorder">内存用量记录器</param>
		/// <param name="pricing">价格配置</param>
		/// <param name="options">评估参数</param>
		/// <param name="goldenSet">金标集</param>
		/// <param name="anchorIndex">锚点映射</param>
		public EvalRunner(
			IRagService ragService,
			ILlmChatService chatService,
			CollectingUsageRecorder usageRecorder,
			IOptions<AiPricingOptions> pricing,
			EvalOptions options,
			GoldenSet goldenSet,
			AnchorIndex anchorIndex)
		{
			_ragService = ragService;
			_chatService = chatService;
			_usageRecorder = usageRecorder;
			_pricing = pricing.Value;
			_options = options;
			_goldenSet = goldenSet;
			_anchorIndex = anchorIndex;
		}

		/// <summary>
		/// 依次运行全部模式
		/// </summary>
		/// <param name="metadata">运行元信息</param>
		/// <param name="corpus">语料清单</param>
		/// <param name="cancellationToken">取消令牌</param>
		/// <returns>完整评估结果</returns>
		public async Task<EvalRunResult> RunAsync(
			EvalMetadata metadata,
			IReadOnlyList<CorpusManifestEntry> corpus,
			CancellationToken cancellationToken)
		{
			var questions = _options.QuestionLimit.HasValue
				? _goldenSet.Questions.Take(_options.QuestionLimit.Value).ToList()
				: _goldenSet.Questions;

			var result = new EvalRunResult
			{
				Metadata = metadata,
				Corpus = [.. corpus]
			};

			foreach (var mode in _options.Modes)
			{
				var modeResult = new EvalModeResult
				{
					Mode = FormatMode(mode),
					HybridEnabled = mode != EvalModeEnum.Vector,
					RewriteEnabled = mode == EvalModeEnum.HybridRewrite && !_options.RetrievalOnly,
					GenerationEnabled = !_options.RetrievalOnly
				};

				for (var index = 0; index < questions.Count; index++)
				{
					var question = questions[index];
					var questionResult = await RunQuestionAsync(mode, question, cancellationToken);
					modeResult.Questions.Add(questionResult);

					Console.WriteLine(
						$"[{modeResult.Mode}] {index + 1}/{questions.Count} {question.Id} R@5={questionResult.RecallAt5:F3} " +
						$"MRR@10={questionResult.MrrAt10:F3} nDCG@5={questionResult.NdcgAt5:F3} {questionResult.LatencyMs}ms" +
						(questionResult.Error is null ? string.Empty : $" 失败：{questionResult.Error}"));

					if (_options.DelayMs > 0 && index < questions.Count - 1)
					{
						await Task.Delay(_options.DelayMs, cancellationToken);
					}
				}

				modeResult.Summary = Summarize(modeResult.Questions);
				result.Modes.Add(modeResult);

				Console.WriteLine(
					$"[{modeResult.Mode}] 汇总：Recall@5={modeResult.Summary.AverageRecallAt5:F3} " +
					$"MRR@10={modeResult.Summary.AverageMrrAt10:F3} nDCG@5={modeResult.Summary.AverageNdcgAt5:F3} " +
					$"tokens={modeResult.Summary.TotalTokens} 成本=¥{modeResult.Summary.TotalEstimatedCostCny:F6}");
			}

			return result;
		}

		private async Task<EvalQuestionResult> RunQuestionAsync(
			EvalModeEnum mode,
			GoldenQuestion question,
			CancellationToken cancellationToken)
		{
			var result = new EvalQuestionResult
			{
				QuestionId = question.Id,
				Category = question.Category,
				Question = question.Question,
				Answerable = question.Answerable,
				GoldChunkIds = [.. _anchorIndex.GoldByQuestion[question.Id]]
			};

			_usageRecorder.Reset();
			var stopwatch = Stopwatch.StartNew();

			try
			{
				var request = new RagRequest
				{
					KnowledgeBaseIds = [_options.KnowledgeBaseId],
					TenantId = _options.TenantId,
					AskId = Guid.NewGuid(),
					Question = question.Question,
					TopK = _options.TopK,
					HybridEnabled = mode != EvalModeEnum.Vector,
					RewriteEnabled = mode == EvalModeEnum.HybridRewrite && !_options.RetrievalOnly
				};

				var retrievalWatch = Stopwatch.StartNew();
				var retrieval = await _ragService.RetrieveAsync(request, cancellationToken);
				retrievalWatch.Stop();

				result.RetrievalLatencyMs = ToMilliseconds(retrievalWatch);
				result.RewriteQueries = [.. retrieval.RewriteQueries.Skip(1)];
				result.RetrievedChunkIds = [.. retrieval.Chunks.Select(ResolveChunkId)];

				if (!_options.RetrievalOnly)
				{
					await GenerateAndScoreCitationsAsync(request, retrieval.Chunks, question, result, cancellationToken);
				}
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				throw;
			}
			catch (Exception exception)
			{
				result.Error = exception.Message;
			}
			finally
			{
				stopwatch.Stop();
				result.LatencyMs = ToMilliseconds(stopwatch);
				ApplyUsage(result);
			}

			if (question.Answerable && result.Error is null)
			{
				result.RecallAt5 = RankingMetrics.RecallAtK(result.RetrievedChunkIds, result.GoldChunkIds, 5);
				result.MrrAt10 = RankingMetrics.MrrAtK(result.RetrievedChunkIds, result.GoldChunkIds, 10);
				result.NdcgAt5 = RankingMetrics.NdcgAtK(result.RetrievedChunkIds, result.GoldChunkIds, 5);
			}

			return result;
		}

		private async Task GenerateAndScoreCitationsAsync(
			RagRequest request,
			IReadOnlyList<RagRetrievedChunk> chunks,
			GoldenQuestion question,
			EvalQuestionResult result,
			CancellationToken cancellationToken)
		{
			var (system, messages) = RagPromptBuilder.Build(request.Question, chunks);
			var stopwatch = Stopwatch.StartNew();
			var completion = await _chatService.CompleteAsync(system, messages, AnswerMaxTokens, 0, cancellationToken);
			stopwatch.Stop();

			result.Answer = completion.Content.Trim();
			await _usageRecorder.RecordAsync(new AiUsageEntry
			{
				KnowledgeBaseId = _options.KnowledgeBaseId,
				AskId = request.AskId,
				Operation = AiOperationEnum.Chat,
				Model = completion.Model,
				PromptTokens = completion.Usage?.PromptTokens
					?? TokenEstimator.Estimate(system) + messages.Sum(message => TokenEstimator.Estimate(message.Content)),
				CompletionTokens = completion.Usage?.CompletionTokens ?? TokenEstimator.Estimate(result.Answer),
				LatencyMs = ToMilliseconds(stopwatch),
				IsEstimated = completion.Usage is null
			}, cancellationToken);

			var citedChunkIds = new List<Guid>();
			foreach (var citation in CitationParser.Parse(result.Answer))
			{
				// 越界编号是模型幻觉，直接忽略
				if (citation > chunks.Count) continue;
				citedChunkIds.Add(ResolveChunkId(chunks[citation - 1]));
			}

			result.CitedChunkIds = citedChunkIds;
			result.CitationCount = citedChunkIds.Count;

			if (!question.Answerable)
			{
				result.IsRefusal = IsRefusal(result.Answer);
				return;
			}

			if (citedChunkIds.Count == 0) return;

			var gold = result.GoldChunkIds.ToHashSet();
			var hits = citedChunkIds.Count(gold.Contains);
			result.CitationHit = hits > 0;
			result.CitationPrecision = (double)hits / citedChunkIds.Count;
		}

		private Guid ResolveChunkId(RagRetrievedChunk chunk)
			=> _anchorIndex.ChunkIdByPosition.TryGetValue((chunk.DocumentId, chunk.ChunkIndex), out var id)
				? id
				: throw new InvalidOperationException($"检索结果无法映射到切片：{chunk.DocumentTitle} #{chunk.ChunkIndex}");

		private void ApplyUsage(EvalQuestionResult result)
		{
			var entries = _usageRecorder.Snapshot();
			result.TotalTokens = entries.Sum(entry => entry.PromptTokens + entry.CompletionTokens);
			result.EstimatedCostCny = entries.Sum(entry =>
				AiUsageCostCalculator.Calculate(entry.Operation, entry.PromptTokens, entry.CompletionTokens, _pricing));
		}

		private static EvalSummary Summarize(IReadOnlyList<EvalQuestionResult> questions)
		{
			var answerable = questions.Where(question => question.Answerable && question.Error is null).ToList();
			var unanswerable = questions.Where(question => !question.Answerable && question.Error is null).ToList();
			var cited = answerable.Where(question => question.CitationCount > 0).ToList();

			return new EvalSummary
			{
				AnswerableCount = answerable.Count,
				UnanswerableCount = unanswerable.Count,
				FailedCount = questions.Count(question => question.Error is not null),
				AverageRecallAt5 = Average(answerable, question => question.RecallAt5),
				AverageMrrAt10 = Average(answerable, question => question.MrrAt10),
				AverageNdcgAt5 = Average(answerable, question => question.NdcgAt5),
				CitationEvaluatedCount = cited.Count,
				CitationHitRate = Average(cited, question => question.CitationHit == true ? 1 : 0),
				CitationPrecision = Average(cited, question => question.CitationPrecision ?? 0),
				NoAnswerRate = unanswerable.Count == 0 ? 0 : unanswerable.Count(question => question.IsRefusal == true) / (double)unanswerable.Count,
				AverageLatencyMs = questions.Count == 0 ? 0 : questions.Average(question => question.LatencyMs),
				TotalTokens = questions.Sum(question => question.TotalTokens),
				TotalEstimatedCostCny = questions.Sum(question => question.EstimatedCostCny)
			};
		}

		private static double Average(IReadOnlyList<EvalQuestionResult> questions, Func<EvalQuestionResult, double> selector)
			=> questions.Count == 0 ? 0 : questions.Average(selector);

		private static bool IsRefusal(string? answer)
			=> !string.IsNullOrWhiteSpace(answer) && RefusalMarkers.Any(marker => answer.Contains(marker, StringComparison.Ordinal));

		private static int ToMilliseconds(Stopwatch stopwatch) => (int)Math.Min(stopwatch.ElapsedMilliseconds, int.MaxValue);

		private static string FormatMode(EvalModeEnum mode) => mode switch
		{
			EvalModeEnum.Vector => "vector",
			EvalModeEnum.Hybrid => "hybrid",
			EvalModeEnum.HybridRewrite => "hybrid-rewrite",
			_ => mode.ToString().ToLowerInvariant()
		};
	}
}
