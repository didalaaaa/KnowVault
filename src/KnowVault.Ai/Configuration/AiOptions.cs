namespace KnowVault.Ai
{

	/// <summary>
	/// AI 模型配置（跨厂商混合路由）。
	/// 环境变量参照 <c>.env.example</c>（DeepSeek/Qwen 的 Key 严禁进仓库）。
	/// </summary>
	public class AiOptions
	{
		// ---- DeepSeek：生成（回答） ----
		public string DeepSeekApiKey { get; set; } = string.Empty;
		public string DeepSeekBaseUrl { get; set; } = "https://api.deepseek.com";
		public string DeepSeekModel { get; set; } = "deepseek-chat";

		// ---- Qwen / 阿里云百炼：Embedding + Rerank ----
		public string QwenApiKey { get; set; } = string.Empty;
		public string QwenBaseUrl { get; set; } = "https://dashscope.aliyuncs.com/compatible-mode/v1";
		public string QwenEmbeddingModel { get; set; } = "text-embedding-v3";
		/// <summary>
		/// 重排服务的基础地址
		/// 重排不属于 OpenAI 兼容模式，不能复用 QwenBaseUrl（那是 /compatible-mode/v1）
		/// </summary>
		public string QwenRerankBaseUrl { get; set; } = "https://dashscope.aliyuncs.com/api/v1";

		/// <summary>
		/// 重排模型名
		/// gte-rerank 已于 2026-05-30 下线（实测 HTTP 403），改用 qwen3-rerank
		/// </summary>
		public string QwenRerankModel { get; set; } = "qwen3-rerank";

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
	}

}
