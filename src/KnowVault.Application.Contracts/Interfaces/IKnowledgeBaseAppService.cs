using Volo.Abp.Application.Services;

namespace KnowVault.Application.Contracts
{
	/// <summary>知识库应用服务（列表 + 默认库 + P2 的增改删）。</summary>
	public interface IKnowledgeBaseAppService : IApplicationService
	{
		/// <summary>列出当前租户可见的知识库。</summary>
		/// <param name="cancellationToken">取消令牌。</param>
		/// <returns>知识库列表，默认知识库排在最前。</returns>
		Task<List<KnowledgeBaseDto>> GetListAsync(CancellationToken cancellationToken);

		/// <summary>取默认知识库（前端首屏选中用）。</summary>
		/// <param name="cancellationToken">取消令牌。</param>
		/// <returns>默认知识库；不存在时返回 null。</returns>
		Task<KnowledgeBaseDto?> GetDefaultAsync(CancellationToken cancellationToken);

		/// <summary>新建知识库。</summary>
		/// <param name="input">名称与描述。</param>
		/// <param name="cancellationToken">取消令牌。</param>
		/// <returns>新建的知识库。</returns>
		Task<KnowledgeBaseDto> CreateAsync(CreateKnowledgeBaseInput input, CancellationToken cancellationToken);

		/// <summary>修改知识库的名称与描述。</summary>
		/// <param name="input">知识库 ID 与新名称、描述。</param>
		/// <param name="cancellationToken">取消令牌。</param>
		/// <returns>修改后的知识库。</returns>
		Task<KnowledgeBaseDto> UpdateAsync(UpdateKnowledgeBaseInput input, CancellationToken cancellationToken);

		/// <summary>删除知识库；默认知识库与非空知识库不可删除。</summary>
		/// <param name="id">知识库 ID。</param>
		/// <param name="cancellationToken">取消令牌。</param>
		/// <returns>任务。</returns>
		Task DeleteAsync(Guid id, CancellationToken cancellationToken);
	}
}
