using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Volo.Abp.DependencyInjection;

namespace KnowVault.Ai
{
	/// <summary>
	/// DeepSeek 生成实现：用 Semantic Kernel 的 OpenAI 兼容连接器指向 DeepSeek 的 /chat/completions。
	/// </summary>
	[ExposeServices(typeof(ILlmChatService))]
	public class DeepSeekChatService : ILlmChatService, ITransientDependency
	{
		private readonly Kernel _kernel;
		private readonly ILogger<DeepSeekChatService> _logger;

		/// <summary>构造函数。</summary>
		/// <param name="kernel">由 Ai 模块构建的 Semantic Kernel 实例。</param>
		/// <param name="logger">日志。</param>
		public DeepSeekChatService(Kernel kernel, ILogger<DeepSeekChatService> logger)
		{
			_kernel = kernel;
			_logger = logger;
		}

		/// <inheritdoc />
		public async Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
		{
			var chat = _kernel.GetRequiredService<IChatCompletionService>();
			var history = new ChatHistory();
			history.AddSystemMessage(systemPrompt);
			history.AddUserMessage(userPrompt);

			var result = await chat.GetChatMessageContentAsync(history, cancellationToken: cancellationToken);
			var answer = result.Content?.Trim();
			if (string.IsNullOrWhiteSpace(answer))
			{
				throw new AiCallException("DeepSeek 返回了空回答。");
			}

			_logger.LogDebug("DeepSeek 生成完成：{Length} 字符", answer.Length);
			return answer;
		}
	}
}
