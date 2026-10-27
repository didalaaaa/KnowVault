using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace KnowVault.Eval
{
	/// <summary>
	/// 生成 Markdown 与 JSON 评估报告
	/// </summary>
	public class EvaluationReportWriter
	{
		private static readonly JsonSerializerOptions JsonOptions = new()
		{
			WriteIndented = true,
			Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
		};

		private static readonly string[] CategoryOrder = ["keyword", "semantic", "cross-document", "unanswerable"];

		private readonly EvalOptions _options;

		/// <summary>
		/// 构造函数
		/// </summary>
		/// <param name="options">评估参数</param>
		public EvaluationReportWriter(EvalOptions options)
		{
			_options = options;
		}

		/// <summary>
		/// 读取语料目录并生成清单
		/// </summary>
		/// <param name="corpusDirectory">语料目录</param>
		/// <returns>语料清单</returns>
		public static IReadOnlyList<CorpusManifestEntry> BuildCorpusManifest(string corpusDirectory)
		{
			var entries = new List<CorpusManifestEntry>();

			foreach (var file in Directory.EnumerateFiles(corpusDirectory, "*.md").OrderBy(path => path, StringComparer.Ordinal))
			{
				var text = File.ReadAllText(file);
				entries.Add(new CorpusManifestEntry
				{
					File = Path.GetFileName(file),
					Title = Path.GetFileNameWithoutExtension(file),
					Characters = text.Replace("\r\n", "\n").Length,
					Sha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant()
				});
			}

			return entries;
		}

		/// <summary>
		/// 写入报告
		/// </summary>
		/// <param name="result">评估结果</param>
		/// <param name="cancellationToken">取消令牌</param>
		/// <returns>Markdown 与 JSON 的绝对路径</returns>
		public async Task<(string MarkdownPath, string JsonPath)> WriteAsync(
			EvalRunResult result,
			CancellationToken cancellationToken)
		{
			var markdownPath = Path.GetFullPath(_options.OutputPath);
			var jsonPath = Path.ChangeExtension(markdownPath, ".json");
			var directory = Path.GetDirectoryName(markdownPath);
			if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);

			await File.WriteAllTextAsync(markdownPath, ToCrlf(BuildMarkdown(result)), new UTF8Encoding(false), cancellationToken);
			await File.WriteAllTextAsync(jsonPath, ToCrlf(JsonSerializer.Serialize(result, JsonOptions)), new UTF8Encoding(false), cancellationToken);

			return (markdownPath, jsonPath);
		}

		/// <summary>
		/// 取当前代码提交，失败时返回 unknown
		/// </summary>
		/// <param name="repositoryRoot">仓库根目录</param>
		/// <returns>短提交号</returns>
		public static string TryGetCommit(string repositoryRoot)
		{
			try
			{
				var startInfo = new ProcessStartInfo("git", "rev-parse --short HEAD")
				{
					WorkingDirectory = repositoryRoot,
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					UseShellExecute = false,
					CreateNoWindow = true
				};

				using var process = Process.Start(startInfo);
				if (process is null) return "unknown";

				var output = process.StandardOutput.ReadToEnd().Trim();
				process.WaitForExit(3000);
				return string.IsNullOrWhiteSpace(output) ? "unknown" : output;
			}
			catch (Exception)
			{
				return "unknown";
			}
		}

		private static string BuildMarkdown(EvalRunResult result)
		{
			var metadata = result.Metadata;
			var builder = new StringBuilder();

			builder.AppendLine($"# KnowVault P5 检索评估报告（{metadata.RunAt[..Math.Min(10, metadata.RunAt.Length)]}）");
			builder.AppendLine();
			builder.AppendLine("> 本报告由 tools/KnowVault.Eval 自动生成；指标只用于横向比较，不代表绝对水平");
			builder.AppendLine();

			AppendMetadata(builder, metadata);
			AppendCorpus(builder, result.Corpus);
			AppendSummary(builder, result.Modes);
			AppendCategorySummary(builder, result.Modes);
			AppendDetail(builder, result.Modes);
			AppendConclusions(builder, result.Modes, metadata.RetrievalOnly);
			AppendFailureCases(builder, result.Modes);
			AppendReproduction(builder, metadata);

			return builder.ToString();
		}

		private static void AppendMetadata(StringBuilder builder, EvalMetadata metadata)
		{
			builder.AppendLine("## 运行元信息");
			builder.AppendLine();
			builder.AppendLine("| 项目 | 值 |");
			builder.AppendLine("|---|---|");
			AppendRow(builder, "运行时间", metadata.RunAt);
			AppendRow(builder, "代码提交", metadata.Commit);
			AppendRow(builder, "知识库 ID", metadata.KnowledgeBaseId);
			AppendRow(builder, "租户 ID", metadata.TenantId);
			AppendRow(builder, "topK", metadata.TopK.ToString(CultureInfo.InvariantCulture));
			AppendRow(builder, "重排", $"{metadata.RerankEnabled}（候选 {metadata.RerankCandidateCount}）");
			AppendRow(builder, "混合权重", $"dense={metadata.DenseWeight}，keyword={metadata.KeywordWeight}，variant={metadata.VariantWeight}");
			AppendRow(builder, "RRF", $"K={metadata.RrfK}，每查询每分支候选={metadata.PerQueryCandidateCount}");
			AppendRow(builder, "查询改写", $"MaxVariants={metadata.QueryRewriteMaxVariants}，MaxTokens={metadata.QueryRewriteMaxTokens}");
			AppendRow(builder, "生成模型", metadata.DeepSeekModel);
			AppendRow(builder, "向量模型", metadata.EmbeddingModel);
			AppendRow(builder, "重排模型", metadata.RerankModel);
			AppendRow(builder, "Qdrant", $"{metadata.QdrantEndpoint} / {metadata.QdrantCollection}");
			AppendRow(builder, "切片参数", $"{metadata.ChunkSizeChars} 字符 / 重叠 {metadata.ChunkOverlapChars}");
			AppendRow(builder, "价格（元 / 百万 token）", $"DeepSeek 输入 {metadata.DeepSeekInputPerMillion}，输出 {metadata.DeepSeekOutputPerMillion}；Embedding {metadata.EmbeddingPerMillion}；Rerank {metadata.RerankPerMillion}");
			AppendRow(builder, "题目间隔", $"{metadata.DelayMs} ms");
			AppendRow(builder, "只跑检索", metadata.RetrievalOnly.ToString());
			AppendRow(builder, "题目上限", metadata.QuestionLimit?.ToString(CultureInfo.InvariantCulture) ?? "全量");
			builder.AppendLine();
		}

		private static void AppendCorpus(StringBuilder builder, IReadOnlyList<CorpusManifestEntry> corpus)
		{
			builder.AppendLine("## 语料清单");
			builder.AppendLine();
			builder.AppendLine("| 文件 | 标题 | 字符数 | SHA256（前 16 位） |");
			builder.AppendLine("|---|---|---:|---|");
			foreach (var entry in corpus)
			{
				builder.AppendLine($"| {Escape(entry.File)} | {Escape(entry.Title)} | {entry.Characters} | `{entry.Sha256[..Math.Min(16, entry.Sha256.Length)]}` |");
			}

			builder.AppendLine();
		}

		private static void AppendSummary(StringBuilder builder, IReadOnlyList<EvalModeResult> modes)
		{
			builder.AppendLine("## 汇总");
			builder.AppendLine();
			builder.AppendLine("| 模式 | 可回答 | 不可回答 | 失败 | Recall@5 | MRR@10 | nDCG@5 | 引用命中率 | 引用精确率 | 弃答率 | 平均延迟(ms) | Tokens | 估算成本(¥) |");
			builder.AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|");
			foreach (var mode in modes)
			{
				var summary = mode.Summary;
				builder.AppendLine(
					$"| {mode.Mode} | {summary.AnswerableCount} | {summary.UnanswerableCount} | {summary.FailedCount} | " +
					$"{Number(summary.AverageRecallAt5)} | {Number(summary.AverageMrrAt10)} | {Number(summary.AverageNdcgAt5)} | " +
					$"{Citation(summary.CitationHitRate, mode.GenerationEnabled, summary.CitationEvaluatedCount)} | " +
					$"{Citation(summary.CitationPrecision, mode.GenerationEnabled, summary.CitationEvaluatedCount)} | " +
					$"{Citation(summary.NoAnswerRate, mode.GenerationEnabled, summary.UnanswerableCount)} | " +
					$"{summary.AverageLatencyMs:F0} | {summary.TotalTokens} | {summary.TotalEstimatedCostCny.ToString("F6", CultureInfo.InvariantCulture)} |");
			}

			builder.AppendLine();
			builder.AppendLine("说明：引用指标只统计至少有一条引用的可回答回答；弃答率只统计不可回答题；只跑检索时这两类指标显示为 —");
			builder.AppendLine();
		}

		private static void AppendCategorySummary(StringBuilder builder, IReadOnlyList<EvalModeResult> modes)
		{
			builder.AppendLine("## 分类指标");
			builder.AppendLine();
			builder.AppendLine("| 模式 | 类别 | 题数 | Recall@5 | MRR@10 | nDCG@5 | 弃答率 |");
			builder.AppendLine("|---|---|---:|---:|---:|---:|---:|");
			foreach (var mode in modes)
			{
				foreach (var category in CategoryOrder)
				{
					var rows = mode.Questions
						.Where(question => string.Equals(question.Category, category, StringComparison.OrdinalIgnoreCase) && question.Error is null)
						.ToList();
					if (rows.Count == 0) continue;

					var isAnswerable = !string.Equals(category, "unanswerable", StringComparison.OrdinalIgnoreCase);
					var recall = isAnswerable ? Number(rows.Average(question => question.RecallAt5)) : "—";
					var mrr = isAnswerable ? Number(rows.Average(question => question.MrrAt10)) : "—";
					var ndcg = isAnswerable ? Number(rows.Average(question => question.NdcgAt5)) : "—";
					var refusal = !isAnswerable && mode.GenerationEnabled
						? $"{rows.Count(question => question.IsRefusal == true) / (double)rows.Count * 100:F1}%"
						: "—";

					builder.AppendLine($"| {mode.Mode} | {category} | {rows.Count} | {recall} | {mrr} | {ndcg} | {refusal} |");
				}
			}

			builder.AppendLine();
		}

		private static void AppendDetail(StringBuilder builder, IReadOnlyList<EvalModeResult> modes)
		{
			builder.AppendLine("## 逐题结果");
			builder.AppendLine();

			foreach (var mode in modes)
			{
				builder.AppendLine($"### {mode.Mode}");
				builder.AppendLine();
				builder.AppendLine("| 题号 | 类别 | 问题 | 金标命中排名 | R@5 | MRR@10 | nDCG@5 | 引用数 | 延迟(ms) | Tokens | 成本(¥) | 备注 |");
				builder.AppendLine("|---|---|---|---|---:|---:|---:|---:|---:|---:|---:|---|");
				foreach (var question in mode.Questions)
				{
					var ranks = FormatGoldRanks(question.RetrievedChunkIds, question.GoldChunkIds);
					var recall = question.Answerable ? Number(question.RecallAt5) : "—";
					var mrr = question.Answerable ? Number(question.MrrAt10) : "—";
					var ndcg = question.Answerable ? Number(question.NdcgAt5) : "—";
					var citations = mode.GenerationEnabled ? question.CitationCount.ToString(CultureInfo.InvariantCulture) : "—";
					var note = BuildNote(question, mode.GenerationEnabled);
					builder.AppendLine(
						$"| {question.QuestionId} | {question.Category} | {Escape(Truncate(question.Question, 40))} | {ranks} | " +
						$"{recall} | {mrr} | {ndcg} | {citations} | {question.LatencyMs} | {question.TotalTokens} | " +
						$"{question.EstimatedCostCny.ToString("F6", CultureInfo.InvariantCulture)} | {note} |");
				}

				builder.AppendLine();
			}
		}

		private static void AppendConclusions(StringBuilder builder, IReadOnlyList<EvalModeResult> modes, bool retrievalOnly)
		{
			builder.AppendLine("## 结论");
			builder.AppendLine();

			if (retrievalOnly)
			{
				builder.AppendLine("- 本次只跑检索，未调用 DeepSeek；引用指标与弃答率不参与比较");
			}

			var vector = modes.FirstOrDefault(mode => mode.Mode == "vector");
			var hybrid = modes.FirstOrDefault(mode => mode.Mode == "hybrid");
			var rewrite = modes.FirstOrDefault(mode => mode.Mode == "hybrid-rewrite");

			AppendComparison(builder, "B 对 A：混合检索 vs 纯向量", vector, hybrid);
			AppendComparison(builder, "C 对 B：混合检索加改写 vs 混合检索", hybrid, rewrite);

			if (vector is null || hybrid is null)
			{
				builder.AppendLine("- 缺少 vector 或 hybrid 模式，无法给出 B 对 A 的对比");
			}

			if (hybrid is null || rewrite is null)
			{
				builder.AppendLine("- 缺少 hybrid 或 hybrid-rewrite 模式，无法给出 C 对 B 的对比");
			}

			builder.AppendLine("- 提升或下降都以本次 30 题、6 篇语料为准；样本小，单题波动会放大到平均值");
			builder.AppendLine();
		}

		private static void AppendComparison(StringBuilder builder, string title, EvalModeResult? baseline, EvalModeResult? candidate)
		{
			builder.AppendLine($"### {title}");
			builder.AppendLine();
			if (baseline is null || candidate is null)
			{
				builder.AppendLine("- 未运行该组，无对比数据");
				builder.AppendLine();
				return;
			}

			var baselineSummary = baseline.Summary;
			var candidateSummary = candidate.Summary;
			var recallDelta = candidateSummary.AverageRecallAt5 - baselineSummary.AverageRecallAt5;
			var mrrDelta = candidateSummary.AverageMrrAt10 - baselineSummary.AverageMrrAt10;
			var ndcgDelta = candidateSummary.AverageNdcgAt5 - baselineSummary.AverageNdcgAt5;
			var latencyDelta = candidateSummary.AverageLatencyMs - baselineSummary.AverageLatencyMs;
			var tokenDelta = candidateSummary.TotalTokens - baselineSummary.TotalTokens;
			var costDelta = candidateSummary.TotalEstimatedCostCny - baselineSummary.TotalEstimatedCostCny;

			builder.AppendLine($"- Recall@5：{Number(baselineSummary.AverageRecallAt5)} → {Number(candidateSummary.AverageRecallAt5)}（{Signed(recallDelta)}）");
			builder.AppendLine($"- MRR@10：{Number(baselineSummary.AverageMrrAt10)} → {Number(candidateSummary.AverageMrrAt10)}（{Signed(mrrDelta)}）");
			builder.AppendLine($"- nDCG@5：{Number(baselineSummary.AverageNdcgAt5)} → {Number(candidateSummary.AverageNdcgAt5)}（{Signed(ndcgDelta)}）");
			builder.AppendLine($"- 平均延迟：{baselineSummary.AverageLatencyMs:F0} ms → {candidateSummary.AverageLatencyMs:F0} ms（{Signed(latencyDelta)} ms）");
			builder.AppendLine($"- Tokens：{baselineSummary.TotalTokens} → {candidateSummary.TotalTokens}（{Signed((double)tokenDelta)}）");
			builder.AppendLine($"- 估算成本：¥{baselineSummary.TotalEstimatedCostCny:F6} → ¥{candidateSummary.TotalEstimatedCostCny:F6}（{Signed(costDelta)}）");

			if (baseline.GenerationEnabled && candidate.GenerationEnabled && baselineSummary.CitationEvaluatedCount > 0 && candidateSummary.CitationEvaluatedCount > 0)
			{
				builder.AppendLine($"- 引用命中率：{Number(baselineSummary.CitationHitRate)} → {Number(candidateSummary.CitationHitRate)}（{Signed(candidateSummary.CitationHitRate - baselineSummary.CitationHitRate)}）");
				builder.AppendLine($"- 引用精确率：{Number(baselineSummary.CitationPrecision)} → {Number(candidateSummary.CitationPrecision)}（{Signed(candidateSummary.CitationPrecision - baselineSummary.CitationPrecision)}）");
			}

			builder.AppendLine($"- 判断：{Judge(recallDelta, ndcgDelta)}");
			builder.AppendLine();
		}

		private static void AppendFailureCases(StringBuilder builder, IReadOnlyList<EvalModeResult> modes)
		{
			builder.AppendLine("## 失败与可疑案例");
			builder.AppendLine();

			var errors = modes.SelectMany(mode => mode.Questions
				.Where(question => question.Error is not null)
				.Select(question => $"- {mode.Mode} / {question.QuestionId}：{question.Error}"))
				.ToList();

			if (errors.Count > 0)
			{
				builder.AppendLine("### 调用失败");
				builder.AppendLine();
				foreach (var error in errors) builder.AppendLine(error);
				builder.AppendLine();
			}

			var misses = modes.SelectMany(mode => mode.Questions
				.Where(question => question.Answerable && question.Error is null && question.RecallAt5 == 0)
				.Select(question => $"- {mode.Mode} / {question.QuestionId}：{question.Question}（检索到 {question.RetrievedChunkIds.Count} 条，未命中金标）"))
				.ToList();

			if (misses.Count > 0)
			{
				builder.AppendLine("### 可回答题未命中金标");
				builder.AppendLine();
				foreach (var miss in misses) builder.AppendLine(miss);
				builder.AppendLine();
			}

			var refusals = modes.SelectMany(mode => mode.Questions
				.Where(question => !question.Answerable && question.Error is null && question.IsRefusal == false)
				.Select(question => $"- {mode.Mode} / {question.QuestionId}：{question.Question}（未明确弃答，回答摘录：{Escape(Truncate(question.Answer ?? string.Empty, 80))}）"))
				.ToList();

			if (refusals.Count > 0)
			{
				builder.AppendLine("### 不可回答题未明确弃答");
				builder.AppendLine();
				foreach (var refusal in refusals) builder.AppendLine(refusal);
				builder.AppendLine();
			}

			if (errors.Count == 0 && misses.Count == 0 && refusals.Count == 0)
			{
				builder.AppendLine("- 本次没有调用失败、金标未命中或未弃答案例");
				builder.AppendLine();
			}
		}

		private static void AppendReproduction(StringBuilder builder, EvalMetadata metadata)
		{
			builder.AppendLine("## 复现");
			builder.AppendLine();
			builder.AppendLine("```bash");
			builder.AppendLine("dotnet run --project tools/KnowVault.Eval -- \\");
			builder.AppendLine($"  --knowledge-base-id {metadata.KnowledgeBaseId} \\");
			builder.AppendLine("  --modes vector,hybrid,hybrid-rewrite");
			builder.AppendLine("```");
			builder.AppendLine();
			builder.AppendLine("- 前置条件：评估知识库已上传 docs/eval/corpus 下全部 6 篇语料并处理为 Ready");
			builder.AppendLine("- API Key 只从环境变量读取：Ai__SiliconFlowApiKey、Ai__DeepSeekApiKey");
			builder.AppendLine("- 只跑检索可加 --retrieval-only；调试可加 --question-limit 5");
			builder.AppendLine("- 限制：生成模型与网络状态会影响延迟与引用指标；检索指标在配置固定时可重复");
			builder.AppendLine();
		}

		private static string BuildNote(EvalQuestionResult question, bool generationEnabled)
		{
			if (question.Error is not null) return $"错误：{Escape(Truncate(question.Error, 60))}";
			if (!question.Answerable && generationEnabled)
			{
				return question.IsRefusal == true ? "明确弃答" : "未明确弃答";
			}

			if (question.Answerable && question.RecallAt5 == 0) return "未命中金标";
			return "—";
		}

		private static string FormatGoldRanks(IReadOnlyList<Guid> retrieved, IReadOnlyList<Guid> gold)
		{
			if (gold.Count == 0) return "—";

			var goldSet = gold.ToHashSet();
			var ranks = retrieved
				.Select((id, index) => new { id, rank = index + 1 })
				.Where(item => goldSet.Contains(item.id))
				.Select(item => item.rank)
				.ToList();

			return ranks.Count == 0 ? "未命中" : string.Join(",", ranks);
		}

		private static string Citation(double value, bool enabled, int sampleCount)
			=> enabled && sampleCount > 0 ? $"{value * 100:F1}%" : "—";

		private static string Number(double value) => value.ToString("F3", CultureInfo.InvariantCulture);

		private static string Signed(double value) => value >= 0 ? $"+{value:F3}" : value.ToString("F3", CultureInfo.InvariantCulture);

		private static string Signed(decimal value) => value >= 0 ? $"+{value:F6}" : value.ToString("F6", CultureInfo.InvariantCulture);

		private static string Judge(double recallDelta, double ndcgDelta)
		{
			if (recallDelta > 0.01 || ndcgDelta > 0.01) return "检索质量有提升";
			if (recallDelta < -0.01 || ndcgDelta < -0.01) return "检索质量下降，需要检查召回或融合配置";
			return "基本持平；若开关开启仍无提升，可能是语料关键词覆盖有限或重排已吸收差异";
		}

		private static void AppendRow(StringBuilder builder, string name, string value)
			=> builder.AppendLine($"| {Escape(name)} | {Escape(value)} |");

		private static string Truncate(string value, int maxLength)
			=> value.Length <= maxLength ? value : value[..maxLength] + "…";

		private static string Escape(string value) => value.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");

		private static string ToCrlf(string text) => text.Replace("\r\n", "\n").Replace("\n", "\r\n");
	}
}
