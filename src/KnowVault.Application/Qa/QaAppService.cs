using System.Text;
using KnowVault.Application.Contracts;
using Microsoft.AspNetCore.Authorization;

namespace KnowVault.Application
{

	/// <summary>
	/// 问答应用服务：把流式编排（<see cref="IQaStreamService"/>）的事件流拼成一次性回答
	/// ABP 动态生成 POST /api/app/qa/ask，与非流式客户端保持 P1 契约
	/// </summary>
	[Authorize]
	public class QaAppService : KnowVaultAppService, IQaAppService
	{
		private readonly IQaStreamService _qaStreamService;

		/// <summary>
		/// 构造函数
		/// </summary>
		/// <param name="qaStreamService">流式问答编排</param>
		public QaAppService(IQaStreamService qaStreamService)
		{
			_qaStreamService = qaStreamService;
		}

		/// <inheritdoc />
		public async Task<QaAnswerDto> AskAsync(AskQuestionInput input, CancellationToken cancellationToken)
		{
			var answer = new StringBuilder();
			var conversationId = Guid.Empty;
			IReadOnlyList<RagCitationDto> citations = [];

			await foreach (var streamEvent in _qaStreamService.AskStreamAsync(input, cancellationToken))
			{
				switch (streamEvent.Kind)
				{
					case QaStreamEventKindEnum.Started:
						conversationId = streamEvent.ConversationId ?? Guid.Empty;
						citations = streamEvent.Citations;
						break;
					case QaStreamEventKindEnum.Delta:
						answer.Append(streamEvent.Delta);
						break;
				}
			}

			return new QaAnswerDto
			{
				ConversationId = conversationId,
				Answer = answer.ToString(),
				Citations = citations
			};
		}
	}

}
