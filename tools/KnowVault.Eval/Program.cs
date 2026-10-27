using KnowVault.Ai;
using KnowVault.Domain;
using KnowVault.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace KnowVault.Eval
{
	/// <summary>
	/// 评估工具入口：组装 AI 服务、解析锚点、顺序跑三组模式并写报告
	/// </summary>
	public class Program
	{
		/// <summary>
		/// 主入口
		/// </summary>
		/// <param name="args">命令行参数</param>
		/// <returns>进程退出码</returns>
		public static async Task<int> Main(string[] args)
		{
			try
			{
				var repositoryRoot = EvalOptions.FindRepositoryRoot();
				var options = EvalOptions.Parse(args, repositoryRoot);
				if (options.ShowHelp)
				{
					EvalOptions.PrintUsage();
					return 0;
				}

				using var cancellation = new CancellationTokenSource();
				Console.CancelKeyPress += (_, eventArgs) =>
				{
					eventArgs.Cancel = true;
					cancellation.Cancel();
				};

				return await RunAsync(options, cancellation.Token);
			}
			catch (ArgumentException exception)
			{
				Console.Error.WriteLine(exception.Message);
				EvalOptions.PrintUsage();
				return 2;
			}
			catch (OperationCanceledException)
			{
				Console.Error.WriteLine("评估已取消");
				return 130;
			}
			catch (Exception exception)
			{
				Console.Error.WriteLine($"评估失败：{exception}");
				return 1;
			}
		}

		private static async Task<int> RunAsync(EvalOptions options, CancellationToken cancellationToken)
		{
			var configuration = new ConfigurationBuilder()
				.SetBasePath(options.RepositoryRoot)
				.AddJsonFile(options.ConfigPath, optional: false)
				.AddEnvironmentVariables()
				.Build();

			var aiOptions = configuration.GetSection("Ai").Get<AiOptions>() ?? new AiOptions();
			ValidateKeys(aiOptions, options.RetrievalOnly);

			var services = new ServiceCollection();
			ConfigureServices(services, configuration, options);
			await using var provider = services.BuildServiceProvider();

			var goldenSet = GoldenSetLoader.Load(options.GoldenSetPath);
			var corpus = EvaluationReportWriter.BuildCorpusManifest(options.CorpusDirectory);
			var vectorStore = provider.GetRequiredService<IVectorStore>();

			Console.WriteLine($"解析金标锚点：{options.KnowledgeBaseId}");
			var anchorIndex = await AnchorResolver.ResolveAsync(
				vectorStore,
				options.KnowledgeBaseId,
				options.TenantId,
				goldenSet.Questions,
				options.CorpusDirectory,
				cancellationToken);
			Console.WriteLine($"锚点解析完成：{anchorIndex.Chunks.Count} 个切片，{goldenSet.Questions.Count} 道题");

			var metadata = BuildMetadata(options, configuration, provider);
			var runner = new EvalRunner(
				provider.GetRequiredService<IRagService>(),
				provider.GetRequiredService<ILlmChatService>(),
				provider.GetRequiredService<CollectingUsageRecorder>(),
				provider.GetRequiredService<IOptions<AiPricingOptions>>(),
				options,
				goldenSet,
				anchorIndex);

			var result = await runner.RunAsync(metadata, corpus, cancellationToken);
			var writer = new EvaluationReportWriter(options);
			var (markdownPath, jsonPath) = await writer.WriteAsync(result, cancellationToken);

			Console.WriteLine($"报告已写入：{markdownPath}");
			Console.WriteLine($"原始结果：{jsonPath}");
			return 0;
		}

		private static void ConfigureServices(IServiceCollection services, IConfiguration configuration, EvalOptions options)
		{
			services.AddLogging(builder => builder
				.AddSimpleConsole(console =>
				{
					console.SingleLine = true;
					console.TimestampFormat = "HH:mm:ss ";
				})
				.SetMinimumLevel(LogLevel.Warning));

			services.Configure<AiOptions>(configuration.GetSection("Ai"));
			services.Configure<QdrantOptions>(configuration.GetSection("Qdrant"));
			services.Configure<AiPricingOptions>(configuration.GetSection("Ai:Pricing"));
			services.AddHttpClient();

			services.AddSingleton(provider =>
			{
				var qdrant = provider.GetRequiredService<IOptions<QdrantOptions>>().Value;
				var clientConfiguration = new ClientConfiguration();
				if (!string.IsNullOrWhiteSpace(qdrant.ApiKey)) clientConfiguration.ApiKey = qdrant.ApiKey!;

				var channel = QdrantChannel.ForAddress(qdrant.Endpoint, clientConfiguration);
				return new QdrantClient(new QdrantGrpcClient(channel));
			});

			services.AddSingleton<IVectorStore, QdrantVectorStore>();
			services.AddTransient<IEmbeddingService, SiliconFlowEmbeddingService>();
			services.AddTransient<IRerankService, SiliconFlowRerankService>();
			services.AddTransient<IRagService, RagService>();
			services.AddSingleton<CollectingUsageRecorder>();
			services.AddSingleton<IAiUsageRecorder>(provider => provider.GetRequiredService<CollectingUsageRecorder>());

			if (options.RetrievalOnly)
			{
				// 检索模式不调用生成模型，用占位实现满足 RagService 构造依赖
				services.AddSingleton<ILlmChatService, DisabledChatService>();
				services.AddSingleton<IQueryRewriteService, DisabledQueryRewriteService>();
				return;
			}

			services.AddSingleton(provider =>
			{
				var ai = provider.GetRequiredService<IOptions<AiOptions>>().Value;
				var builder = Kernel.CreateBuilder();
				builder.AddOpenAIChatCompletion(ai.DeepSeekModel, new Uri(ai.DeepSeekBaseUrl), ai.DeepSeekApiKey);
				return builder.Build();
			});
			services.AddTransient<ILlmChatService, DeepSeekChatService>();
			services.AddTransient<IQueryRewriteService, QueryRewriteService>();
		}

		private static EvalMetadata BuildMetadata(EvalOptions options, IConfiguration configuration, IServiceProvider provider)
		{
			var ai = provider.GetRequiredService<IOptions<AiOptions>>().Value;
			var qdrant = provider.GetRequiredService<IOptions<QdrantOptions>>().Value;
			var pricing = provider.GetRequiredService<IOptions<AiPricingOptions>>().Value;

			return new EvalMetadata
			{
				RunAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
				Commit = EvaluationReportWriter.TryGetCommit(options.RepositoryRoot),
				KnowledgeBaseId = options.KnowledgeBaseId.ToString(),
				TenantId = options.TenantId.ToString(),
				TopK = options.TopK,
				RerankEnabled = ai.RerankEnabled,
				RerankCandidateCount = ai.RerankCandidateCount,
				DenseWeight = ai.Hybrid.DenseWeight,
				KeywordWeight = ai.Hybrid.KeywordWeight,
				VariantWeight = ai.Hybrid.VariantWeight,
				RrfK = ai.Hybrid.RrfK,
				PerQueryCandidateCount = ai.Hybrid.PerQueryCandidateCount,
				QueryRewriteMaxVariants = ai.QueryRewrite.MaxVariants,
				QueryRewriteMaxTokens = ai.QueryRewrite.MaxTokens,
				DeepSeekModel = ai.DeepSeekModel,
				EmbeddingModel = ai.SiliconFlowEmbeddingModel,
				RerankModel = ai.SiliconFlowRerankModel,
				QdrantCollection = qdrant.CollectionName,
				QdrantEndpoint = qdrant.Endpoint,
				ChunkSizeChars = configuration.GetValue("Ingest:ChunkSizeChars", 1000),
				ChunkOverlapChars = configuration.GetValue("Ingest:ChunkOverlapChars", 120),
				DeepSeekInputPerMillion = pricing.DeepSeekInputPerMillion,
				DeepSeekOutputPerMillion = pricing.DeepSeekOutputPerMillion,
				EmbeddingPerMillion = pricing.EmbeddingPerMillion,
				RerankPerMillion = pricing.RerankPerMillion,
				DelayMs = options.DelayMs,
				RetrievalOnly = options.RetrievalOnly,
				QuestionLimit = options.QuestionLimit,
				GoldenSetPath = options.GoldenSetPath,
				CorpusDirectory = options.CorpusDirectory
			};
		}

		private static void ValidateKeys(AiOptions ai, bool retrievalOnly)
		{
			if (string.IsNullOrWhiteSpace(ai.SiliconFlowApiKey))
			{
				throw new InvalidOperationException("缺少 SiliconFlow API Key：请设置环境变量 Ai__SiliconFlowApiKey");
			}

			if (!retrievalOnly && string.IsNullOrWhiteSpace(ai.DeepSeekApiKey))
			{
				throw new InvalidOperationException("缺少 DeepSeek API Key：请设置环境变量 Ai__DeepSeekApiKey，或加 --retrieval-only");
			}
		}

		private sealed class DisabledChatService : ILlmChatService
		{
			public IAsyncEnumerable<LlmStreamChunk> CompleteStreamAsync(
				string systemPrompt,
				IReadOnlyList<LlmChatMessage> messages,
				CancellationToken cancellationToken = default)
				=> throw new InvalidOperationException("检索评估模式不调用生成模型");

			public Task<LlmCompletionResult> CompleteAsync(
				string systemPrompt,
				IReadOnlyList<LlmChatMessage> messages,
				int maxTokens,
				double temperature,
				CancellationToken cancellationToken = default)
				=> throw new InvalidOperationException("检索评估模式不调用生成模型");
		}

		private sealed class DisabledQueryRewriteService : IQueryRewriteService
		{
			public Task<QueryRewriteResult> RewriteAsync(
				string question,
				IReadOnlyList<LlmChatMessage> history,
				CancellationToken cancellationToken = default)
				=> Task.FromResult(new QueryRewriteResult { Queries = [], Model = "disabled" });
		}
	}
}
