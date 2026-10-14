using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace KnowVault.Ai
{
	/// <summary>
	/// Qwen（阿里云百炼文本重排接口）重排实现
	/// </summary>
	/// <remarks>
	/// 重排不属于 OpenAI 兼容模式，端点与模型由独立配置项给出（见 <see cref="AiOptions.QwenRerankBaseUrl"/>）；
	/// DashScope 在鉴权失败等场景可能返回 200 + 错误体，因此解析为空时还要检查 code 字段
	/// </remarks>
	[ExposeServices(typeof(IRerankService))]
	public class QwenRerankService : IRerankService, ITransientDependency
	{
		// 与 Embedding 不同：重排走 DashScope 原生服务路径，不是 OpenAI 兼容端点
		private const string RerankPath = "services/rerank/text-rerank/text-rerank";

		private readonly AiOptions _options;
		private readonly IHttpClientFactory _httpClientFactory;
		private readonly ILogger<QwenRerankService> _logger;

		/// <summary>
		/// 构造函数
		/// </summary>
		/// <param name="options">AI 配置（重排 BaseUrl / 模型 / Key）</param>
		/// <param name="httpClientFactory">HTTP 客户端工厂</param>
		/// <param name="logger">日志</param>
		public QwenRerankService(
			IOptions<AiOptions> options,
			IHttpClientFactory httpClientFactory,
			ILogger<QwenRerankService> logger)
		{
			_options = options.Value;
			_httpClientFactory = httpClientFactory;
			_logger = logger;
		}

		/// <inheritdoc />
		public async Task<IReadOnlyList<RerankResult>> RerankAsync(
			string query,
			IReadOnlyList<string> documents,
			int topN,
			CancellationToken cancellationToken = default)
		{
			if (documents.Count == 0) return [];

			EnsureConfigured();

			var client = _httpClientFactory.CreateClient(nameof(QwenRerankService));
			using var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.QwenRerankBaseUrl.TrimEnd('/')}/{RerankPath}");
			request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.QwenApiKey);
			request.Content = JsonContent.Create(new
			{
				model = _options.QwenRerankModel,
				input = new { query, documents },
				parameters = new
				{
					// top_n 不能超过候选数，否则部分服务实现会报参数错误
					top_n = Math.Clamp(topN, 1, documents.Count),
					return_documents = false
				}
			});

			using var response = await client.SendAsync(request, cancellationToken);
			var body = await response.Content.ReadAsStringAsync(cancellationToken);
			if (!response.IsSuccessStatusCode)
			{
				throw new AiCallException($"Qwen rerank 调用失败：HTTP {(int)response.StatusCode} {body}");
			}

			var results = QwenRerankResponseParser.Parse(body);
			if (results.Count == 0 && body.Contains("\"code\"", StringComparison.Ordinal))
			{
				throw new AiCallException($"Qwen rerank 返回错误：{body}");
			}

			_logger.LogDebug("Qwen rerank 完成：候选 {Candidates} 条，返回 {Count} 条", documents.Count, results.Count);
			return results;
		}

		private void EnsureConfigured()
		{
			if (string.IsNullOrWhiteSpace(_options.QwenApiKey))
			{
				throw new AiCallException("未配置 Qwen API Key（环境变量 Ai__QwenApiKey）。");
			}
		}
	}
}
