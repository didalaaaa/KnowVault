using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KnowVault.DemoSeed
{
	/// <summary>
	/// 通过公开 HTTP API 幂等导入演示知识库与 7 份样例文档。
	/// 只依赖 BCL，不引用 ABP；Compose 里作为一次性任务运行。
	/// </summary>
	internal static class Program
	{
		private const string TokenUrl = "/connect/token";
		private const string ClientId = "KnowVault_App";
		private const string KnowledgeBaseUrl = "/api/app/knowledge-base";
		private const string DocumentUrl = "/api/app/document";

		private static readonly JsonSerializerOptions JsonOptions = new()
		{
			PropertyNameCaseInsensitive = true
		};

		/// <summary>
		/// 入口：等待 API → 登录 → 建库 → 上传样例 → 轮询处理状态。
		/// </summary>
		/// <param name="args">未使用的命令行参数</param>
		/// <returns>0 表示成功或可跳过的失败；1 表示 API 不可用/超时</returns>
		private static async Task<int> Main(string[] args)
		{
			using var cancellationTokenSource = new CancellationTokenSource();
			Console.CancelKeyPress += (_, eventArgs) =>
			{
				eventArgs.Cancel = true;
				cancellationTokenSource.Cancel();
			};

			var cancellationToken = cancellationTokenSource.Token;
			var settings = DemoSettings.FromEnvironment();
			Console.WriteLine($"[demo-seed] API: {settings.ApiBaseUrl}");

			using var httpClient = new HttpClient
			{
				BaseAddress = new Uri(settings.ApiBaseUrl.TrimEnd('/') + "/"),
				Timeout = TimeSpan.FromSeconds(120)
			};

			if (!await WaitForHealthyAsync(httpClient, settings.WaitTimeoutSeconds, cancellationToken))
			{
				Console.Error.WriteLine($"[demo-seed] 等待 API 健康超时（{settings.WaitTimeoutSeconds}s），请检查 docker compose logs api");
				return 1;
			}

			if (!await LoginAsync(httpClient, settings, cancellationToken))
			{
				return 1;
			}

			var knowledgeBase = await FindOrCreateKnowledgeBaseAsync(httpClient, settings, cancellationToken);
			if (knowledgeBase is null)
			{
				return 1;
			}

			if (string.IsNullOrWhiteSpace(settings.SiliconFlowApiKey))
			{
				Console.WriteLine("[demo-seed] 未配置 SILICONFLOW_API_KEY，跳过示例文档导入；配置 Key 后重新运行：");
				Console.WriteLine("[demo-seed]   docker compose run --rm demo-seed");
				return 0;
			}

			var sampleFiles = GetSampleFiles(settings.SamplesDir);
			if (sampleFiles.Count == 0)
			{
				Console.Error.WriteLine($"[demo-seed] 样例目录为空或不存在：{settings.SamplesDir}");
				return 1;
			}

			var existingDocuments = await GetDocumentsAsync(httpClient, knowledgeBase.Id, cancellationToken);
			var uploaded = 0;
			var skipped = 0;

			foreach (var file in sampleFiles)
			{
				var title = Path.GetFileNameWithoutExtension(file);
				if (existingDocuments.Any(document => string.Equals(document.Title, title, StringComparison.OrdinalIgnoreCase)))
				{
					Console.WriteLine($"[demo-seed] 已存在，跳过：{title}");
					skipped++;
					continue;
				}

				var result = await UploadAsync(httpClient, knowledgeBase.Id, file, title, cancellationToken);
				if (result == UploadResultEnum.Uploaded)
				{
					Console.WriteLine($"[demo-seed] 已上传：{title}");
					uploaded++;
				}
				else if (result == UploadResultEnum.Duplicate)
				{
					Console.WriteLine($"[demo-seed] 服务端已存在同名文件，跳过：{title}");
					skipped++;
				}
				else
				{
					Console.Error.WriteLine($"[demo-seed] 上传失败：{title}");
				}
			}

			Console.WriteLine($"[demo-seed] 上传完成：新增 {uploaded}，跳过 {skipped}；开始等待解析与向量化");

			var finalDocuments = await WaitForIngestAsync(httpClient, knowledgeBase.Id, settings.IngestTimeoutSeconds, cancellationToken);
			if (finalDocuments is null)
			{
				Console.Error.WriteLine($"[demo-seed] 等待文档处理超时（{settings.IngestTimeoutSeconds}s），可稍后在页面查看状态");
				return 1;
			}

			foreach (var document in finalDocuments.OrderBy(document => document.Title))
			{
				var status = document.Status switch
				{
					0 => "Pending",
					1 => "Processing",
					2 => "Ready",
					3 => $"Failed（{document.ErrorMessage}）",
					_ => $"Unknown({document.Status})"
				};
				Console.WriteLine($"[demo-seed] {document.Title}: {status}");
			}

			if (finalDocuments.Any(document => document.Status == 3))
			{
				Console.WriteLine("[demo-seed] 有文档处理失败，请检查模型 Key 与 API 日志；不影响其它文档使用");
			}

			return 0;
		}

		private static async Task<bool> WaitForHealthyAsync(HttpClient httpClient, int timeoutSeconds, CancellationToken cancellationToken)
		{
			var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
			while (DateTime.UtcNow < deadline)
			{
				try
				{
					using var response = await httpClient.GetAsync("health-status", cancellationToken);
					if (response.IsSuccessStatusCode)
					{
						Console.WriteLine("[demo-seed] API 已健康");
						return true;
					}
				}
				catch (HttpRequestException)
				{
					// API 还没起来，继续等
				}

				await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
			}

			return false;
		}

		private static async Task<bool> LoginAsync(HttpClient httpClient, DemoSettings settings, CancellationToken cancellationToken)
		{
			using var form = new FormUrlEncodedContent(new Dictionary<string, string>
			{
				["grant_type"] = "password",
				["client_id"] = ClientId,
				["username"] = settings.UserName,
				["password"] = settings.Password,
				["scope"] = "openid profile email roles KnowVault offline_access"
			});

			using var response = await httpClient.PostAsync(TokenUrl, form, cancellationToken);
			if (!response.IsSuccessStatusCode)
			{
				var body = await response.Content.ReadAsStringAsync(cancellationToken);
				Console.Error.WriteLine($"[demo-seed] 登录失败：{(int)response.StatusCode} {body}");
				return false;
			}

			var token = await response.Content.ReadFromJsonAsync<TokenResponse>(JsonOptions, cancellationToken);
			if (string.IsNullOrWhiteSpace(token?.AccessToken))
			{
				Console.Error.WriteLine("[demo-seed] 登录响应中没有 access_token");
				return false;
			}

			httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
			Console.WriteLine($"[demo-seed] 已登录：{settings.UserName}");
			return true;
		}

		private static async Task<KnowledgeBaseItem?> FindOrCreateKnowledgeBaseAsync(
			HttpClient httpClient,
			DemoSettings settings,
			CancellationToken cancellationToken)
		{
			var knowledgeBases = await httpClient.GetFromJsonAsync<List<KnowledgeBaseItem>>(KnowledgeBaseUrl, JsonOptions, cancellationToken);
			var existing = knowledgeBases?.FirstOrDefault(knowledgeBase =>
				string.Equals(knowledgeBase.Name?.Trim(), settings.KnowledgeBaseName, StringComparison.OrdinalIgnoreCase));

			if (existing is not null)
			{
				Console.WriteLine($"[demo-seed] 使用已有知识库：{existing.Name}（{existing.Id}）");
				return existing;
			}

			using var response = await httpClient.PostAsJsonAsync(
				KnowledgeBaseUrl,
				new
				{
					name = settings.KnowledgeBaseName,
					description = "用于演示多格式文档、混合检索与引用溯源；由 demo-seed 自动创建"
				},
				cancellationToken);

			if (!response.IsSuccessStatusCode)
			{
				var body = await response.Content.ReadAsStringAsync(cancellationToken);
				Console.Error.WriteLine($"[demo-seed] 创建知识库失败：{(int)response.StatusCode} {body}");
				return null;
			}

			var created = await response.Content.ReadFromJsonAsync<KnowledgeBaseItem>(JsonOptions, cancellationToken);
			Console.WriteLine($"[demo-seed] 已创建知识库：{created?.Name}（{created?.Id}）");
			return created;
		}

		private static async Task<List<DocumentItem>> GetDocumentsAsync(
			HttpClient httpClient,
			Guid knowledgeBaseId,
			CancellationToken cancellationToken)
		{
			var documents = await httpClient.GetFromJsonAsync<List<DocumentItem>>(
				$"{DocumentUrl}?knowledgeBaseId={knowledgeBaseId}",
				JsonOptions,
				cancellationToken);
			return documents ?? [];
		}

		private static async Task<UploadResultEnum> UploadAsync(
			HttpClient httpClient,
			Guid knowledgeBaseId,
			string filePath,
			string title,
			CancellationToken cancellationToken)
		{
			await using var fileStream = File.OpenRead(filePath);
			using var content = new MultipartFormDataContent();
			content.Add(new StringContent(knowledgeBaseId.ToString()), "knowledgeBaseId");
			content.Add(new StringContent(title), "title");

			var fileContent = new StreamContent(fileStream);
			fileContent.Headers.ContentType = new MediaTypeHeaderValue(GetContentType(Path.GetExtension(filePath)));
			content.Add(fileContent, "file", Path.GetFileName(filePath));

			using var response = await httpClient.PostAsync($"{DocumentUrl}/upload", content, cancellationToken);
			if (response.IsSuccessStatusCode) return UploadResultEnum.Uploaded;

			var body = await response.Content.ReadAsStringAsync(cancellationToken);
			if (response.StatusCode == HttpStatusCode.BadRequest && body.Contains("已存在同名文件", StringComparison.Ordinal))
			{
				return UploadResultEnum.Duplicate;
			}

			Console.Error.WriteLine($"[demo-seed] 上传 {Path.GetFileName(filePath)} 失败：{(int)response.StatusCode} {body}");
			return UploadResultEnum.Failed;
		}

		private static async Task<List<DocumentItem>?> WaitForIngestAsync(
			HttpClient httpClient,
			Guid knowledgeBaseId,
			int timeoutSeconds,
			CancellationToken cancellationToken)
		{
			var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
			while (DateTime.UtcNow < deadline)
			{
				var documents = await GetDocumentsAsync(httpClient, knowledgeBaseId, cancellationToken);
				if (documents.Count > 0 && documents.All(document => document.Status is 2 or 3))
				{
					return documents;
				}

				await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
			}

			return null;
		}

		private static List<string> GetSampleFiles(string samplesDirectory)
		{
			if (!Directory.Exists(samplesDirectory)) return [];

			return Directory
				.EnumerateFiles(samplesDirectory)
				.Where(file => !Path.GetFileName(file).StartsWith('.'))
				.OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
				.ToList();
		}

		private static string GetContentType(string extension) => extension.ToLowerInvariant() switch
		{
			".pdf" => "application/pdf",
			".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
			".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
			".md" or ".markdown" => "text/markdown",
			".html" or ".htm" => "text/html",
			_ => "application/octet-stream"
		};

		private enum UploadResultEnum
		{
			Uploaded,
			Duplicate,
			Failed
		}

		private sealed class DemoSettings
		{
			public string ApiBaseUrl { get; init; } = "http://localhost:8081";
			public string UserName { get; init; } = "demo";
			public string Password { get; init; } = "1q2w3E*";
			public string KnowledgeBaseName { get; init; } = "KnowVault 示例库";
			public string SamplesDir { get; init; } = "/samples";
			public string SiliconFlowApiKey { get; init; } = string.Empty;
			public int WaitTimeoutSeconds { get; init; } = 120;
			public int IngestTimeoutSeconds { get; init; } = 600;

			public static DemoSettings FromEnvironment() => new()
			{
				ApiBaseUrl = GetValue("API_BASE_URL", "http://localhost:8081"),
				UserName = GetValue("DEMO_USERNAME", "demo"),
				Password = GetValue("DEMO_PASSWORD", "1q2w3E*"),
				KnowledgeBaseName = GetValue("KNOWLEDGE_BASE_NAME", "KnowVault 示例库"),
				SamplesDir = GetValue("SAMPLES_DIR", "/samples"),
				SiliconFlowApiKey = GetValue("SILICONFLOW_API_KEY", string.Empty),
				WaitTimeoutSeconds = GetIntValue("WAIT_TIMEOUT_SECONDS", 120),
				IngestTimeoutSeconds = GetIntValue("INGEST_TIMEOUT_SECONDS", 600)
			};

			private static string GetValue(string name, string defaultValue)
			{
				var value = Environment.GetEnvironmentVariable(name);
				return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
			}

			private static int GetIntValue(string name, int defaultValue) =>
				int.TryParse(Environment.GetEnvironmentVariable(name), out var value) ? value : defaultValue;
		}

		private sealed class TokenResponse
		{
			[JsonPropertyName("access_token")]
			public string? AccessToken { get; set; }
		}

		private sealed class KnowledgeBaseItem
		{
			public Guid Id { get; set; }
			public string? Name { get; set; }
		}

		private sealed class DocumentItem
		{
			public Guid Id { get; set; }
			public string? Title { get; set; }
			public int Status { get; set; }
			public string? ErrorMessage { get; set; }
		}
	}
}
