namespace KnowVault.Application.Contracts
{
	/// <summary>
	/// 知识库成员管理服务
	/// </summary>
	public interface IKnowledgeBaseMemberAppService
	{
		/// <summary>
		/// 取知识库成员列表
		/// </summary>
		/// <param name="knowledgeBaseId">知识库 ID</param>
		/// <param name="cancellationToken">取消令牌</param>
		/// <returns>成员列表</returns>
		Task<List<KnowledgeBaseMemberDto>> GetListAsync(Guid knowledgeBaseId, CancellationToken cancellationToken);

		/// <summary>
		/// 新增成员或调整角色
		/// </summary>
		/// <param name="input">成员与角色</param>
		/// <param name="cancellationToken">取消令牌</param>
		/// <returns>成员视图</returns>
		Task<KnowledgeBaseMemberDto> SetAsync(SetKnowledgeBaseMemberInput input, CancellationToken cancellationToken);

		/// <summary>
		/// 移除成员
		/// </summary>
		/// <param name="knowledgeBaseId">知识库 ID</param>
		/// <param name="userId">用户 ID</param>
		/// <param name="cancellationToken">取消令牌</param>
		/// <returns>任务</returns>
		Task RemoveAsync(Guid knowledgeBaseId, Guid userId, CancellationToken cancellationToken);
	}
}
