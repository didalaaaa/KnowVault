using Volo.Abp.Application.Services;

namespace KnowVault.Application.Contracts
{

	/// <summary>
	/// 文档应用服务（上传/列文档）。P0 记录元数据待处理；上传真实文件到 MinIO 在 P1。
	/// ABP 动态生成 POST /api/app/documents、GET /api/app/documents?knowledgeBaseId=...
	/// </summary>
	public interface IDocumentAppService : IApplicationService
	{
		Task<DocumentDto> CreateAsync(CreateDocumentInput input);

		Task<List<DocumentDto>> GetListAsync(Guid knowledgeBaseId);
	}

}
