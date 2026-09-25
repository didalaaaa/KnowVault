using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace KnowVault.Ai
{

	/// <summary>
	/// P0 骨架实现：契约与依赖就绪，返回占位回答。
	/// P1 在此接入 Qdrant 检索 + DeepSeek 生成 + 引用溯源。
	/// </summary>
	public class RagService : IRagService, ITransientDependency
	{
		private readonly AiOptions _options;
		private readonly ILogger<RagService> _logger;

		public RagService(IOptions<AiOptions> options, ILogger<RagService> logger)
		{
			_options = options.Value;
			_logger = logger;
		}

		public Task<RagAnswer> AnswerAsync(RagRequest request, CancellationToken cancellationToken = default)
		{
			_logger.LogInformation(
				"RAG 占位：知识库 {KnowledgeBaseId} / 会话 {ConversationId} / TopK {TopK} / 模型 {DeepSeekModel}",
				request.KnowledgeBaseId, request.ConversationId, request.TopK, _options.DeepSeekModel);

			return Task.FromResult(RagAnswer.Placeholder(
				$"这是 P0 骨架的占位回答。知识库 [{request.KnowledgeBaseId}] 的 RAG 检索与生成将在 P1 接入。"));
		}
	}

}
