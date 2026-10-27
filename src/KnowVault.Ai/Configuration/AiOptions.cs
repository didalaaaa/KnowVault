namespace KnowVault.Ai
{

	/// <summary>
	/// AI 模型配置（跨厂商混合路由）
	/// 环境变量参照 <c>.env.example</c>（DeepSeek / SiliconFlow 的 Key 严禁进仓库）
	/// </summary>
	public class AiOptions
	{
		// ---- DeepSeek：生成（回答） ----
		public string DeepSeekApiKey { get; set; } = string.Empty;
		public string DeepSeekBaseUrl { get; set; } = "https://api.deepseek.com";
		public string DeepSeekModel { get; set; } = "deepseek-chat";

		// ---- SiliconFlow：Embedding + Rerank ----
		public string SiliconFlowApiKey { get; set; } = string.Empty;
		public string SiliconFlowBaseUrl { get; set; } = "https://api.siliconflow.cn/v1";
		/// <summary>
		/// 向量模型
		/// bge-m3 固定输出 1024 维，必须与 Qdrant:VectorSize 保持一致
		/// </summary>
		public string SiliconFlowEmbeddingModel { get; set; } = "BAAI/bge-m3";
		/// <summary>
		/// 重排模型
		/// 与 Embedding 共用 SiliconFlowBaseUrl，端点为 /rerank
		/// </summary>
		public string SiliconFlowRerankModel { get; set; } = "BAAI/bge-reranker-v2-m3";

		// ---- 检索参数 ----
		public int TopK { get; set; } = 5;

		/// <summary>
		/// 是否启用重排；关闭时直接使用向量检索顺序
		/// </summary>
		public bool RerankEnabled { get; set; } = true;

		/// <summary>
		/// 重排前从向量库召回的候选条数，实际取值会被收敛到 [topK, 100]
		/// </summary>
		public int RerankCandidateCount { get; set; } = 20;

		/// <summary>
		/// 带入提示词的历史消息条数上限（按时间取最近若干条）
		/// </summary>
		public int HistoryMaxMessages { get; set; } = 6;

		/// <summary>
		/// 是否启用混合检索；关闭时只走向量召回
		/// </summary>
		public bool HybridEnabled { get; set; } = true;

		/// <summary>
		/// 混合检索参数
		/// </summary>
		public HybridOptions Hybrid { get; set; } = new();
	}

}
