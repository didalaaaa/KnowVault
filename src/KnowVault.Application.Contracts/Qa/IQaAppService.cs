using Volo.Abp.Application.Services;

namespace KnowVault.Application.Contracts
{

	/// <summary>
	/// 问答应用服务（RAG 闭环的入口）。P0 提供契约，P1 接入检索与生成。
	/// ABP 动态生成 POST /api/app/qa/ask。
	/// </summary>
	public interface IQaAppService : IApplicationService
	{
		Task<QaAnswerDto> AskAsync(AskQuestionInput input);
	}

}
