using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace KnowVault.Ai
{
	/// <summary>
	/// 查询改写实现：调用一次非流式模型并把输出解析成变体
	/// </summary>
	public class QueryRewriteService : IQueryRewriteService, ITransientDependency
	{
		private const int MaxVariantsLimit = 2;

		private readonly ILlmChatService _chatService;
		private readonly AiOptions _options;
		private readonly ILogger<QueryRewriteService> _logger;

		/// <summary>
		/// 构造函数
		/// </summary>
		/// <param name="chatService">对话模型服务</param>
		/// <param name="options">AI 配置</param>
		/// <param name="logger">日志</param>
		public QueryRewriteService(ILlmChatService chatService, IOptions<AiOptions> options, ILogger<QueryRewriteService> logger)
		{
			_chatService = chatService;
			_options = options.Value;
			_logger = logger;
		}

		/// <inheritdoc />
		public async Task<QueryRewriteResult> RewriteAsync(
			string question,
			IReadOnlyList<LlmChatMessage> history,
			CancellationToken cancellationToken = default)
		{
			var options = _options.QueryRewrite;
			var maxVariants = Math.Clamp(options.MaxVariants, 0, MaxVariantsLimit);

			if (maxVariants == 0)
			{
				return new QueryRewriteResult
				{
					Queries = [],
					Model = _options.DeepSeekModel
				};
			}

			var (systemPrompt, messages) = QueryRewritePromptBuilder.Build(question, history, maxVariants);
			var completion = await _chatService.CompleteAsync(
				systemPrompt,
				messages,
				Math.Max(options.MaxTokens, 1),
				0,
				cancellationToken);

			var queries = QueryRewriteParser.Parse(completion.Content, question, maxVariants);
			_logger.LogDebug("查询改写完成：{Count} 条变体", queries.Count);

			return new QueryRewriteResult
			{
				Queries = queries,
				Model = completion.Model,
				Usage = completion.Usage
			};
		}
	}
}
