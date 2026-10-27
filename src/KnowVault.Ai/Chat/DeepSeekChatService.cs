using System.Diagnostics;
using System.Runtime.CompilerServices;
using KnowVault.Domain.Shared;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using OpenAI.Chat;
using Volo.Abp.DependencyInjection;

namespace KnowVault.Ai
{
	/// <summary>
	/// DeepSeek 生成实现：用 Semantic Kernel 的 OpenAI 兼容连接器指向 DeepSeek 的 /chat/completions
	/// </summary>
	[ExposeServices(typeof(ILlmChatService))]
	public class DeepSeekChatService : ILlmChatService, ITransientDependency
	{
		private static readonly ActivitySource ActivitySource = new(KnowVaultTelemetry.ActivitySourceName);

		private readonly Kernel _kernel;
		private readonly AiOptions _options;
		private readonly ILogger<DeepSeekChatService> _logger;

		/// <summary>
		/// 构造函数
		/// </summary>
		/// <param name="kernel">由 Ai 模块构建的 Semantic Kernel 实例</param>
		/// <param name="options">AI 配置（模型名）</param>
		/// <param name="logger">日志</param>
		public DeepSeekChatService(Kernel kernel, IOptions<AiOptions> options, ILogger<DeepSeekChatService> logger)
		{
			_kernel = kernel;
			_options = options.Value;
			_logger = logger;
		}

		/// <inheritdoc />
		public async IAsyncEnumerable<LlmStreamChunk> CompleteStreamAsync(string systemPrompt, IReadOnlyList<LlmChatMessage> messages, [EnumeratorCancellation] CancellationToken cancellationToken = default)
		{
			using var activity = ActivitySource.StartActivity("llm.complete");
			activity?.SetTag("llm.model", _options.DeepSeekModel);
			activity?.SetTag("llm.history_count", messages.Count);

			var chat = _kernel.GetRequiredService<IChatCompletionService>();
			var history = BuildHistory(systemPrompt, messages);

			// DeepSeek 默认不回传流式 usage，显式打开 OpenAI 的 stream_options；
			// ExtraBody 仍标记为 SK 实验 API，但这是当前唯一能透传该字段的入口
#pragma warning disable SKEXP0010
			var settings = new OpenAIPromptExecutionSettings
			{
				ExtraBody = new Dictionary<string, object?>
				{
					["$.stream_options.include_usage"] = true
				}
			};
#pragma warning restore SKEXP0010

			var deltaCount = 0;
			var stream = chat.GetStreamingChatMessageContentsAsync(history, settings, _kernel, cancellationToken);
			await foreach (var item in stream.WithCancellation(cancellationToken))
			{
				var usage = TryGetUsage(item.Metadata);

				if (!string.IsNullOrEmpty(item.Content))
				{
					deltaCount++;
					yield return new LlmStreamChunk { Delta = item.Content, Usage = usage };
					continue;
				}

				// usage 通常只在最后一帧出现且内容为空，不能按空帧丢弃
				if (usage is not null) yield return new LlmStreamChunk { Usage = usage };
			}

			_logger.LogDebug("DeepSeek 流式生成完成：{Count} 个增量片段", deltaCount);
		}

		/// <inheritdoc />
		public async Task<LlmCompletionResult> CompleteAsync(
			string systemPrompt,
			IReadOnlyList<LlmChatMessage> messages,
			int maxTokens,
			double temperature,
			CancellationToken cancellationToken = default)
		{
			using var activity = ActivitySource.StartActivity("llm.complete");
			activity?.SetTag("llm.model", _options.DeepSeekModel);
			activity?.SetTag("llm.history_count", messages.Count);
			activity?.SetTag("llm.max_tokens", maxTokens);
			activity?.SetTag("llm.temperature", temperature);

			var chat = _kernel.GetRequiredService<IChatCompletionService>();
			var history = BuildHistory(systemPrompt, messages);
			var settings = new OpenAIPromptExecutionSettings
			{
				MaxTokens = maxTokens,
				Temperature = temperature
			};

			var result = await chat.GetChatMessageContentAsync(history, settings, _kernel, cancellationToken);

			return new LlmCompletionResult
			{
				Content = result.Content ?? string.Empty,
				Model = result.ModelId ?? _options.DeepSeekModel,
				Usage = TryGetUsage(result.Metadata)
			};
		}

		private static ChatHistory BuildHistory(string systemPrompt, IReadOnlyList<LlmChatMessage> messages)
		{
			var history = new ChatHistory();
			history.AddSystemMessage(systemPrompt);

			foreach (var message in messages)
			{
				history.AddMessage(message.Role == MessageRoleEnum.User ? AuthorRole.User : AuthorRole.Assistant, message.Content);
			}

			return history;
		}

		private static LlmUsage? TryGetUsage(IReadOnlyDictionary<string, object?>? metadata)
		{
			if (metadata is null || !metadata.TryGetValue("Usage", out var value) || value is not ChatTokenUsage usage) return null;

			return new LlmUsage
			{
				PromptTokens = usage.InputTokenCount,
				CompletionTokens = usage.OutputTokenCount
			};
		}
	}
}
