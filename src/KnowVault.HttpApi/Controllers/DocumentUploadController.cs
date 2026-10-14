using KnowVault.Application.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KnowVault.HttpApi
{
	/// <summary>
	/// 文档上传（显式 MVC 控制器）。
	/// 不能用 ABP 动态 API：当 <c>IRemoteStreamContent</c> 嵌在 DTO 里时，约定生成的 action 只声明
	/// <c>application/json</c>（实测 <c>POST /api/app/document/upload</c> 传 multipart 会被拒为 415）。
	/// 这里显式声明 <c>[Consumes]</c> + <c>[FromForm]</c>，交给 ABP 的 RemoteStreamContentModelBinder 绑定文件。
	/// </summary>
	[Route("api/app/document")]
	[Authorize]
	public class DocumentUploadController : KnowVaultController
	{
		private readonly IDocumentAppService _documentAppService;

		/// <summary>构造函数。</summary>
		/// <param name="documentAppService">文档应用服务。</param>
		public DocumentUploadController(IDocumentAppService documentAppService)
		{
			_documentAppService = documentAppService;
		}

		/// <summary>上传文档并触发异步摄入。</summary>
		/// <param name="input">上传入参（multipart/form-data，字段名 file / knowledgeBaseId / title）。</param>
		/// <returns>新建的文档 DTO（状态 Pending）。</returns>
		[HttpPost("upload")]
		[Consumes("multipart/form-data")]
		public Task<DocumentDto> UploadAsync([FromForm] UploadDocumentInput input, CancellationToken cancellationToken)
			=> _documentAppService.UploadAsync(input, cancellationToken);
	}
}
