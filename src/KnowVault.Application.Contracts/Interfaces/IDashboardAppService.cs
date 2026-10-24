using Volo.Abp.Application.Services;

namespace KnowVault.Application.Contracts
{
	/// <summary>
	/// 仪表盘应用服务（概览页数据）
	/// </summary>
	public interface IDashboardAppService : IApplicationService
	{
		/// <summary>
		/// 取当前用户的概览统计
		/// </summary>
		/// <param name="cancellationToken">取消令牌</param>
		/// <returns>仪表盘汇总，无数据时各计数为 0</returns>
		Task<DashboardDto> GetAsync(CancellationToken cancellationToken);
	}
}
