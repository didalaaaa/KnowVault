using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using KnowVault.Domain;
using KnowVault.Domain.Shared;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace KnowVault.Ai
{
	/// <summary>
	/// SiliconFlow Embedding 实现（OpenAI 兼容端点）
	/// 单请求最多 32 条文本，这里按 <see cref="MaxBatchSize"/> 保守分批，并按响应 index 还原顺序
	/// —— 顺序一旦错位，检索会把 A 的向量当成 B 的内容，属于最难排查的脏 bug
	/// </summary>
	public class SiliconFlowEmbeddingService : IEmbeddingService, ITransientDependency
	{
		// 硅基流动单请求上限为 32 条；保守取 10，避免单批 token 过大
		private const int MaxBatchSize = 10;

		private static readonly ActivitySource ActivitySource = new(KnowVaultTelemetry.ActivitySourceName);

		private readonly AiOptions _options;
		private readonly IHttpClientFactory _httpClientFactory;
		private readonly ILogger<SiliconFlowEmbeddingService> _logger;

		/// <summary>
		/// 构造函数
		/// </summary>
		/// <param name="options">AI 配置（模型名、BaseUrl、Key）</param>
		/// <param name="httpClientFactory">HTTP 客户端工厂</param>
		/// <param name="logger">日志</param>
		public SiliconFlowEmbeddingService(
			IOptions<AiOptions> options,
			IHttpClientFactory httpClientFactory,
			ILogger<SiliconFlowEmbeddingService> logger)
		{
			_options = options.Value;
			_httpClientFactory = httpClientFactory;
			_logger = logger;
		}

		/// <inheritdoc />
		public async Task<IReadOnlyList<float[]>> EmbedAsync(
			IReadOnlyList<string> texts,
			CancellationToken cancellationToken = default)
		{
			using var activity = ActivitySource.StartActivity("embedding.embed");
			activity?.SetTag("embedding.batch_size", texts.Count);

			if (texts.Count == 0)
			{
				return Array.Empty<float[]>();
			}

			EnsureConfigured();

			var all = new List<float[]>(texts.Count);
			foreach (var batch in texts.Chunk(MaxBatchSize))
			{
				all.AddRange(await EmbedBatchAsync(batch, cancellationToken));
			}

			return all;
		}

		private async Task<List<float[]>> EmbedBatchAsync(string[] batch, CancellationToken cancellationToken)
		{
			var client = _httpClientFactory.CreateClient(nameof(SiliconFlowEmbeddingService));
			using var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.SiliconFlowBaseUrl.TrimEnd('/')}/embeddings");
			request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.SiliconFlowApiKey);
			request.Content = JsonContent.Create(new
			{
				model = _options.SiliconFlowEmbeddingModel,
				input = batch,
				encoding_format = "float"
				// bge-m3 不支持 dimensions（仅 Qwen3 系列支持），传了会返回 400
			});

			using var response = await client.SendAsync(request, cancellationToken);
			var body = await response.Content.ReadAsStringAsync(cancellationToken);
			if (!response.IsSuccessStatusCode)
			{
				throw new AiCallException($"SiliconFlow embedding 调用失败：HTTP {(int)response.StatusCode} {body}");
			}

			using var document = JsonDocument.Parse(body);
			var data = document.RootElement.GetProperty("data");
			var ordered = new float[data.GetArrayLength()][];
			foreach (var item in data.EnumerateArray())
			{
				var index = item.GetProperty("index").GetInt32();
				var vector = item.GetProperty("embedding").EnumerateArray().Select(x => x.GetSingle()).ToArray();
				ordered[index] = vector;
			}

			if (ordered.Any(x => x is null))
			{
				throw new AiCallException("SiliconFlow embedding 响应缺少部分 index，无法保证顺序对齐。");
			}

			_logger.LogDebug("SiliconFlow embedding 完成一批：{Count} 条，维度 {Dimension}", batch.Length, ordered[0].Length);
			return ordered.ToList();
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
