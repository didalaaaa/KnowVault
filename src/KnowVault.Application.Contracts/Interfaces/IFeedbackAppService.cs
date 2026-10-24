using KnowVault.Domain.Shared;
using Volo.Abp.Application.Services;

namespace KnowVault.Application.Contracts
{
	/// <summary>
	/// 消息反馈应用服务
	/// </summary>
	/// <remarks>
	/// 路由（ABP 动态 API）：POST /api/app/feedback/set（非标准方法名按「服务名/方法名」约定）
	/// </remarks>
	public interface IFeedbackAppService : IApplicationService
	{
		/// <summary>
		/// 设置、切换或取消当前用户对某条助手消息的反馈
		/// </summary>
		/// <param name="input">消息 ID、评价类型（null 表示取消）与可选备注</param>
		/// <param name="cancellationToken">取消令牌</param>
		/// <returns>保存后的评价类型；取消时为 null</returns>
		Task<FeedbackRatingEnum?> SetAsync(SetFeedbackInput input, CancellationToken cancellationToken);
	}
}
