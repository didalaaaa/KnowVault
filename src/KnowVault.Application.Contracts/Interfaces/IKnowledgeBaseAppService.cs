using Volo.Abp.Application.Services;

namespace KnowVault.Application.Contracts
{
	/// <summary>知识库应用服务（P1 只提供列表，管理界面留 P2）。</summary>
	public interface IKnowledgeBaseAppService : IApplicationService
	{
		/// <summary>列出当前租户可见的知识库。</summary>
		/// <returns>知识库列表，默认知识库排在最前。</returns>
		Task<List<KnowledgeBaseDto>> GetListAsync(CancellationToken cancellationToken);

		/// <summary>取默认知识库（前端首屏选中用）。</summary>
		/// <returns>默认知识库；不存在时返回 null。</returns>
		Task<KnowledgeBaseDto?> GetDefaultAsync(CancellationToken cancellationToken);
	}
}
