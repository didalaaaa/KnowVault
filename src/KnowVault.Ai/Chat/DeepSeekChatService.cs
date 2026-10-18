using System.Diagnostics;
using System.Runtime.CompilerServices;
using KnowVault.Domain.Shared;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
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
		public async IAsyncEnumerable<string> CompleteStreamAsync(string systemPrompt, IReadOnlyList<LlmChatMessage> messages, [EnumeratorCancellation] CancellationToken cancellationToken = default)
		{
			using var activity = ActivitySource.StartActivity("llm.complete");
			activity?.SetTag("llm.model", _options.DeepSeekModel);
			activity?.SetTag("llm.history_count", messages.Count);

			var chat = _kernel.GetRequiredService<IChatCompletionService>();
			var history = new ChatHistory();
			history.AddSystemMessage(systemPrompt);
			foreach (var message in messages)
			{
				history.AddMessage(message.Role == MessageRoleEnum.User ? AuthorRole.User : AuthorRole.Assistant, message.Content);
			}

			var deltaCount = 0;
			var stream = chat.GetStreamingChatMessageContentsAsync(history, cancellationToken: cancellationToken);
			await foreach (var item in stream.WithCancellation(cancellationToken))
			{
				// SK 会给出空白的首帧/尾帧，直接丢弃，避免前端出现空跳动
				if (string.IsNullOrEmpty(item.Content)) continue;
				deltaCount++;
				yield return item.Content;
			}

			_logger.LogDebug("DeepSeek 流式生成完成：{Count} 个增量片段", deltaCount);
		}
	}
}
