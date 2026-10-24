using Volo.Abp.Application.Services;

namespace KnowVault.Application.Contracts
{
	/// <summary>
	/// 文档应用服务（上传/查文档）。
	/// 路由（已用 Swagger 核对）：服务名去掉 AppService 后缀为**单数** →
	/// GET /api/app/document?knowledgeBaseId=...、GET /api/app/document/{id}；
	/// 上传走显式控制器 <c>DocumentUploadController</c>：POST /api/app/document/upload（multipart/form-data）。
	/// </summary>
	public interface IDocumentAppService : IApplicationService
	{
		/// <summary>上传文档并触发异步摄入（不暴露为动态 API：实现类方法上标了 RemoteService(false)，改由显式控制器暴露）。</summary>
		/// <param name="input">上传入参（含文件流）。</param>
		/// <returns>新建的文档 DTO（状态为 Pending）。</returns>
		Task<DocumentDto> UploadAsync(UploadDocumentInput input, CancellationToken cancellationToken);

		/// <summary>取单个文档（前端轮询状态用）。</summary>
		/// <param name="id">文档 ID。</param>
		/// <returns>文档 DTO。</returns>
		Task<DocumentDto> GetAsync(Guid id, CancellationToken cancellationToken);

		/// <summary>列出知识库下的文档。</summary>
		/// <param name="knowledgeBaseId">知识库 ID。</param>
		/// <returns>文档列表（按创建时间倒序）。</returns>
		Task<List<DocumentDto>> GetListAsync(Guid knowledgeBaseId, CancellationToken cancellationToken);

		/// <summary>
		/// 删除文档及其切片、向量与原件
		/// </summary>
		/// <param name="id">文档 ID</param>
		/// <param name="cancellationToken">取消令牌</param>
		/// <returns>任务</returns>
		Task DeleteAsync(Guid id, CancellationToken cancellationToken);

		/// <summary>
		/// 重新处理文档：清旧切片/向量后重新排队
		/// </summary>
		/// <param name="id">文档 ID</param>
		/// <param name="cancellationToken">取消令牌</param>
		/// <returns>任务</returns>
		Task ReprocessAsync(Guid id, CancellationToken cancellationToken);

		/// <summary>
		/// 取文档切片（引用溯源与详情页用）
		/// </summary>
		/// <param name="id">文档 ID</param>
		/// <param name="cancellationToken">取消令牌</param>
		/// <returns>按切片序号升序排列的切片列表</returns>
		Task<List<DocumentChunkDto>> GetChunksAsync(Guid id, CancellationToken cancellationToken);

		/// <summary>
		/// 取文档原件内容（不暴露为动态 API，改由 <c>DocumentContentController</c> 输出文件流）
		/// </summary>
		/// <param name="id">文档 ID</param>
		/// <param name="cancellationToken">取消令牌</param>
		/// <returns>原件流、MIME 类型与下载文件名</returns>
		Task<DocumentContentDto> GetContentAsync(Guid id, CancellationToken cancellationToken);
	}
}
