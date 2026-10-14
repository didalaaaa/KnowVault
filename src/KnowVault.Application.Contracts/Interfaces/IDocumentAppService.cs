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
	}
}
