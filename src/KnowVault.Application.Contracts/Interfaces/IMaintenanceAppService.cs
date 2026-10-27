using Volo.Abp.Application.Services;

namespace KnowVault.Application.Contracts
{
	/// <summary>
	/// 运维接口：重建向量库 schema 并重新处理文档
	/// </summary>
	public interface IMaintenanceAppService : IApplicationService
	{
		/// <summary>
		/// 删除并按当前 schema 重建向量 collection，然后把可检索文档重新排队
		/// </summary>
		/// <param name="cancellationToken">取消令牌</param>
		/// <returns>重新排队与跳过的文档统计</returns>
		Task<RebuildVectorIndexResultDto> RebuildVectorIndexAsync(CancellationToken cancellationToken);
	}
}
