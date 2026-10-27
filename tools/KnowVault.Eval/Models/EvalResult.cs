namespace KnowVault.Eval
{
	/// <summary>
	/// 一次评估的完整结果，同时用于 Markdown 与 JSON 报告
	/// </summary>
	public class EvalRunResult
	{
		/// <summary>
		/// 运行元信息
		/// </summary>
		public EvalMetadata Metadata { get; set; } = new();

		/// <summary>
		/// 语料清单
		/// </summary>
		public List<CorpusManifestEntry> Corpus { get; set; } = [];

		/// <summary>
		/// 各模式的结果
		/// </summary>
		public List<EvalModeResult> Modes { get; set; } = [];
	}

	/// <summary>
	/// 评估运行元信息
	/// </summary>
	public class EvalMetadata
	{
		/// <summary>运行时间</summary>
		public string RunAt { get; set; } = string.Empty;

		/// <summary>代码提交</summary>
		public string Commit { get; set; } = string.Empty;

		/// <summary>知识库 ID</summary>
		public string KnowledgeBaseId { get; set; } = string.Empty;

		/// <summary>租户 ID</summary>
		public string TenantId { get; set; } = string.Empty;

		/// <summary>最终引用条数</summary>
		public int TopK { get; set; }

		/// <summary>重排开关</summary>
		public bool RerankEnabled { get; set; }

		/// <summary>重排候选数</summary>
		public int RerankCandidateCount { get; set; }

		/// <summary>向量分支权重</summary>
		public double DenseWeight { get; set; }

		/// <summary>关键词分支权重</summary>
		public double KeywordWeight { get; set; }

		/// <summary>改写查询权重</summary>
		public double VariantWeight { get; set; }

		/// <summary>RRF 平滑常数</summary>
		public int RrfK { get; set; }

		/// <summary>每条查询每分支召回数</summary>
		public int PerQueryCandidateCount { get; set; }

		/// <summary>改写最多变体数</summary>
		public int QueryRewriteMaxVariants { get; set; }

		/// <summary>改写输出 token 上限</summary>
		public int QueryRewriteMaxTokens { get; set; }

		/// <summary>生成模型</summary>
		public string DeepSeekModel { get; set; } = string.Empty;

		/// <summary>向量模型</summary>
		public string EmbeddingModel { get; set; } = string.Empty;

		/// <summary>重排模型</summary>
		public string RerankModel { get; set; } = string.Empty;

		/// <summary>Qdrant collection</summary>
		public string QdrantCollection { get; set; } = string.Empty;

		/// <summary>Qdrant gRPC 端点</summary>
		public string QdrantEndpoint { get; set; } = string.Empty;

		/// <summary>切片容量</summary>
		public int ChunkSizeChars { get; set; }

		/// <summary>切片重叠</summary>
		public int ChunkOverlapChars { get; set; }

		/// <summary>DeepSeek 输入价（元 / 百万 token）</summary>
		public decimal DeepSeekInputPerMillion { get; set; }

		/// <summary>DeepSeek 输出价（元 / 百万 token）</summary>
		public decimal DeepSeekOutputPerMillion { get; set; }

		/// <summary>Embedding 价（元 / 百万 token）</summary>
		public decimal EmbeddingPerMillion { get; set; }

		/// <summary>Rerank 价（元 / 百万 token）</summary>
		public decimal RerankPerMillion { get; set; }

		/// <summary>题目间隔毫秒</summary>
		public int DelayMs { get; set; }

		/// <summary>是否只跑检索</summary>
		public bool RetrievalOnly { get; set; }

		/// <summary>只跑前 N 题；为空表示全量</summary>
		public int? QuestionLimit { get; set; }

		/// <summary>金标集路径</summary>
		public string GoldenSetPath { get; set; } = string.Empty;

		/// <summary>语料目录</summary>
		public string CorpusDirectory { get; set; } = string.Empty;
	}

	/// <summary>
	/// 语料清单项
	/// </summary>
	public class CorpusManifestEntry
	{
		/// <summary>文件名</summary>
		public string File { get; set; } = string.Empty;

		/// <summary>文档标题（文件名去扩展名）</summary>
		public string Title { get; set; } = string.Empty;

		/// <summary>字符数</summary>
		public int Characters { get; set; }

		/// <summary>SHA256</summary>
		public string Sha256 { get; set; } = string.Empty;
	}

	/// <summary>
	/// 单个模式的评估结果
	/// </summary>
	public class EvalModeResult
	{
		/// <summary>模式名</summary>
		public string Mode { get; set; } = string.Empty;

		/// <summary>混合检索是否开启</summary>
		public bool HybridEnabled { get; set; }

		/// <summary>查询改写是否开启</summary>
		public bool RewriteEnabled { get; set; }

		/// <summary>是否运行了生成模型</summary>
		public bool GenerationEnabled { get; set; }

		/// <summary>汇总指标</summary>
		public EvalSummary Summary { get; set; } = new();

		/// <summary>逐题结果</summary>
		public List<EvalQuestionResult> Questions { get; set; } = [];
	}

	/// <summary>
	/// 模式级汇总指标
	/// </summary>
	public class EvalSummary
	{
		/// <summary>可回答题数</summary>
		public int AnswerableCount { get; set; }

		/// <summary>不可回答题数</summary>
		public int UnanswerableCount { get; set; }

		/// <summary>失败题数</summary>
		public int FailedCount { get; set; }

		/// <summary>平均 Recall@5</summary>
		public double AverageRecallAt5 { get; set; }

		/// <summary>平均 MRR@10</summary>
		public double AverageMrrAt10 { get; set; }

		/// <summary>平均 nDCG@5</summary>
		public double AverageNdcgAt5 { get; set; }

		/// <summary>参与引用统计的回答数</summary>
		public int CitationEvaluatedCount { get; set; }

		/// <summary>引用命中率</summary>
		public double CitationHitRate { get; set; }

		/// <summary>引用精确率</summary>
		public double CitationPrecision { get; set; }

		/// <summary>弃答率</summary>
		public double NoAnswerRate { get; set; }

		/// <summary>平均每题耗时（毫秒）</summary>
		public double AverageLatencyMs { get; set; }

		/// <summary>总 token</summary>
		public int TotalTokens { get; set; }

		/// <summary>总估算成本（元）</summary>
		public decimal TotalEstimatedCostCny { get; set; }
	}

	/// <summary>
	/// 单题评估结果
	/// </summary>
	public class EvalQuestionResult
	{
		/// <summary>题号</summary>
		public string QuestionId { get; set; } = string.Empty;

		/// <summary>类别</summary>
		public string Category { get; set; } = string.Empty;

		/// <summary>问题</summary>
		public string Question { get; set; } = string.Empty;

		/// <summary>是否可回答</summary>
		public bool Answerable { get; set; }

		/// <summary>金标切片 ID</summary>
		public List<Guid> GoldChunkIds { get; set; } = [];

		/// <summary>检索到的切片 ID（按相关性降序）</summary>
		public List<Guid> RetrievedChunkIds { get; set; } = [];

		/// <summary>Recall@5</summary>
		public double RecallAt5 { get; set; }

		/// <summary>MRR@10</summary>
		public double MrrAt10 { get; set; }

		/// <summary>nDCG@5</summary>
		public double NdcgAt5 { get; set; }

		/// <summary>实际参与的改写查询（不含原问题）</summary>
		public List<string> RewriteQueries { get; set; } = [];

		/// <summary>模型回答</summary>
		public string? Answer { get; set; }

		/// <summary>引用到的切片 ID</summary>
		public List<Guid> CitedChunkIds { get; set; } = [];

		/// <summary>引用条数</summary>
		public int CitationCount { get; set; }

		/// <summary>是否至少引用一条金标切片；无引用时为空</summary>
		public bool? CitationHit { get; set; }

		/// <summary>引用精确率；无引用时为空</summary>
		public double? CitationPrecision { get; set; }

		/// <summary>不可回答题是否明确弃答</summary>
		public bool? IsRefusal { get; set; }

		/// <summary>检索耗时（毫秒）</summary>
		public int RetrievalLatencyMs { get; set; }

		/// <summary>整题耗时（毫秒）</summary>
		public int LatencyMs { get; set; }

		/// <summary>该题所有 AI 调用 token 数</summary>
		public int TotalTokens { get; set; }

		/// <summary>该题估算成本（元）</summary>
		public decimal EstimatedCostCny { get; set; }

		/// <summary>错误信息；成功为空</summary>
		public string? Error { get; set; }
	}
}
