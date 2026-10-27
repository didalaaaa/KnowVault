using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using KnowVault.Domain.Shared;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace KnowVault.Ai
{
	/// <summary>
	/// SiliconFlow 文本重排实现
	/// </summary>
	/// <remarks>
	/// 与 Embedding 共用 <see cref="AiOptions.SiliconFlowBaseUrl"/>，端点为 /rerank；
	/// 请求把全部候选交给服务端打分，最终保留几条由调用方本地裁剪
	/// </remarks>
	[ExposeServices(typeof(IRerankService))]
	public class SiliconFlowRerankService : IRerankService, ITransientDependency
	{
		private static readonly ActivitySource ActivitySource = new(KnowVaultTelemetry.ActivitySourceName);

		private readonly AiOptions _options;
		private readonly IHttpClientFactory _httpClientFactory;
		private readonly ILogger<SiliconFlowRerankService> _logger;

		/// <summary>
		/// 构造函数
		/// </summary>
		/// <param name="options">AI 配置（BaseUrl / 模型 / Key）</param>
		/// <param name="httpClientFactory">HTTP 客户端工厂</param>
		/// <param name="logger">日志</param>
		public SiliconFlowRerankService(
			IOptions<AiOptions> options,
			IHttpClientFactory httpClientFactory,
			ILogger<SiliconFlowRerankService> logger)
		{
			_options = options.Value;
			_httpClientFactory = httpClientFactory;
			_logger = logger;
		}

		/// <inheritdoc />
		public async Task<RerankServiceResult> RerankAsync(string query, IReadOnlyList<string> documents, CancellationToken cancellationToken = default)
		{
			using var activity = ActivitySource.StartActivity("rerank.rerank");
			activity?.SetTag("rerank.document_count", documents.Count);

			if (documents.Count == 0)
			{
				return new RerankServiceResult
				{
					Results = [],
					Model = _options.SiliconFlowRerankModel
				};
			}

			EnsureConfigured();

			var client = _httpClientFactory.CreateClient(nameof(SiliconFlowRerankService));
			using var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.SiliconFlowBaseUrl.TrimEnd('/')}/rerank");
			request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.SiliconFlowApiKey);
			request.Content = JsonContent.Create(new
			{
				model = _options.SiliconFlowRerankModel,
				query,
				documents,
				// top_n 传全量：服务端只负责打分，最终保留几条由调用方本地裁剪
				top_n = documents.Count,
				return_documents = false
			});

			using var response = await client.SendAsync(request, cancellationToken);
			var body = await response.Content.ReadAsStringAsync(cancellationToken);
			if (!response.IsSuccessStatusCode)
			{
				throw new AiCallException($"SiliconFlow rerank 调用失败：HTTP {(int)response.StatusCode} {body}");
			}

			var results = SiliconFlowRerankResponseParser.Parse(body);
			var inputTokens = SiliconFlowRerankResponseParser.ParseInputTokens(body);
			var totalTokens = inputTokens ?? TokenEstimator.Estimate(query) + documents.Sum(TokenEstimator.Estimate);

			_logger.LogDebug("SiliconFlow rerank 完成：候选 {Candidates} 条，返回 {Count} 条", documents.Count, results.Count);
			return new RerankServiceResult
			{
				Results = results,
				Model = _options.SiliconFlowRerankModel,
				TotalTokens = totalTokens,
				IsEstimated = inputTokens is null
			};
		}

		private void EnsureConfigured()
		{
			if (string.IsNullOrWhiteSpace(_options.SiliconFlowApiKey))
			{
				throw new AiCallException("未配置 SiliconFlow API Key（环境变量 Ai__SiliconFlowApiKey）。");
			}
		}
	}
}
