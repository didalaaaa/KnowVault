using Volo.Abp.Application.Services;

namespace KnowVault.Application.Contracts
{
	/// <summary>
	/// 文档应用服务（上传/查文档）。
	/// ABP 动态生成 POST /api/app/documents/upload、GET /api/app/documents、GET /api/app/documents/{id}。
	/// </summary>
	public interface IDocumentAppService : IApplicationService
	{
		/// <summary>上传文档并触发异步摄入。</summary>
		/// <param name="input">上传入参（含文件流）。</param>
		/// <returns>新建的文档 DTO（状态为 Pending）。</returns>
		Task<DocumentDto> UploadAsync(UploadDocumentInput input);

		/// <summary>取单个文档（前端轮询状态用）。</summary>
		/// <param name="id">文档 ID。</param>
		/// <returns>文档 DTO。</returns>
		Task<DocumentDto> GetAsync(Guid id);

		/// <summary>列出知识库下的文档。</summary>
		/// <param name="knowledgeBaseId">知识库 ID。</param>
		/// <returns>文档列表（按创建时间倒序）。</returns>
		Task<List<DocumentDto>> GetListAsync(Guid knowledgeBaseId);
	}
}
