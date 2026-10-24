using Volo.Abp.Application.Services;

namespace KnowVault.Application.Contracts
{
	/// <summary>
	/// 会话应用服务：列表、消息回放、重命名与删除
	/// </summary>
	/// <remarks>
	/// 路由（ABP 动态 API）：GET /api/app/conversation?knowledgeBaseId=、
	/// GET /api/app/conversation/{id}/messages、PUT /api/app/conversation（会话 ID 在请求体）、
	/// DELETE /api/app/conversation/{id}
	/// </remarks>
	public interface IConversationAppService : IApplicationService
	{
		/// <summary>
		/// 列出当前用户的会话
		/// </summary>
		/// <param name="knowledgeBaseId">按知识库过滤；为空取全部</param>
		/// <param name="cancellationToken">取消令牌</param>
		/// <returns>会话摘要，按最近活动倒序</returns>
		Task<List<ConversationDto>> GetListAsync(Guid? knowledgeBaseId, CancellationToken cancellationToken);

		/// <summary>
		/// 取会话中的全部消息（时间升序）
		/// </summary>
		/// <param name="id">会话 ID</param>
		/// <param name="cancellationToken">取消令牌</param>
		/// <returns>消息列表；引用解析失败时按空列表返回</returns>
		Task<List<ChatMessageDto>> GetMessagesAsync(Guid id, CancellationToken cancellationToken);

		/// <summary>
		/// 重命名会话
		/// </summary>
		/// <param name="input">会话 ID 与新标题</param>
		/// <param name="cancellationToken">取消令牌</param>
		/// <returns>更新后的会话摘要</returns>
		Task<ConversationDto> UpdateAsync(RenameConversationInput input, CancellationToken cancellationToken);

		/// <summary>
		/// 删除会话及其全部消息
		/// </summary>
		/// <param name="id">会话 ID</param>
		/// <param name="cancellationToken">取消令牌</param>
		/// <returns>任务</returns>
		Task DeleteAsync(Guid id, CancellationToken cancellationToken);
	}
}
