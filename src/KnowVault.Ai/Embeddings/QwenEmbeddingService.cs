using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using KnowVault.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace KnowVault.Ai
{
	/// <summary>
	/// Qwen（阿里云百炼 OpenAI 兼容模式）Embedding 实现。
	/// 兼容模式对单请求文本数有上限，这里按 <see cref="MaxBatchSize"/> 分批，并按响应 index 还原顺序
	/// —— 顺序一旦错位，检索会把 A 的向量当成 B 的内容，属于最难排查的脏 bug。
	/// </summary>
	public class QwenEmbeddingService : IEmbeddingService, ITransientDependency
	{
		// 保守批大小：兼容模式上限以实测为准，超限会返回 400，宁可多跑几批
		private const int MaxBatchSize = 10;

		private readonly AiOptions _options;
		private readonly IHttpClientFactory _httpClientFactory;
		private readonly ILogger<QwenEmbeddingService> _logger;

		/// <summary>构造函数。</summary>
		/// <param name="options">AI 配置（模型名、BaseUrl、Key）。</param>
		/// <param name="httpClientFactory">HTTP 客户端工厂。</param>
		/// <param name="logger">日志。</param>
		public QwenEmbeddingService(
			IOptions<AiOptions> options,
			IHttpClientFactory httpClientFactory,
			ILogger<QwenEmbeddingService> logger)
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
			var client = _httpClientFactory.CreateClient(nameof(QwenEmbeddingService));
			using var request = new HttpRequestMessage(
				HttpMethod.Post,
				$"{_options.QwenBaseUrl.TrimEnd('/')}/embeddings");
			request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.QwenApiKey);
			request.Content = JsonContent.Create(new
			{
				model = _options.QwenEmbeddingModel,
				input = batch,
				encoding_format = "float"
			});

			using var response = await client.SendAsync(request, cancellationToken);
			var body = await response.Content.ReadAsStringAsync(cancellationToken);
			if (!response.IsSuccessStatusCode)
			{
				throw new AiCallException($"Qwen embedding 调用失败：HTTP {(int)response.StatusCode} {body}");
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
				throw new AiCallException("Qwen embedding 响应缺少部分 index，无法保证顺序对齐。");
			}

			_logger.LogDebug("Qwen embedding 完成一批：{Count} 条，维度 {Dimension}", batch.Length, ordered[0].Length);
			return ordered.ToList();
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
