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
		public string QwenRerankModel { get; set; } = "gte-rerank";

		// ---- 检索参数 ----
		public int TopK { get; set; } = 5;
	}

}
