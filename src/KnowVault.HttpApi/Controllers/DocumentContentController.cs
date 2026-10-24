using KnowVault.Application.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Entities;

namespace KnowVault.HttpApi
{
	/// <summary>
	/// 文档原件读取（显式 MVC 控制器）。
	/// 应用层的 <c>GetContentAsync</c> 返回 <see cref="Stream"/>，无法由 ABP 动态 API 序列化，
	/// 只能在这里以 <c>FileStreamResult</c> 输出；<c>enableRangeProcessing</c> 让 PDF 预览可以分段加载
	/// </summary>
	[Route("api/app/document")]
	[Authorize]
	public class DocumentContentController : KnowVaultController
	{
		private readonly IDocumentAppService _documentAppService;

		/// <summary>
		/// 构造函数
		/// </summary>
		/// <param name="documentAppService">文档应用服务</param>
		public DocumentContentController(IDocumentAppService documentAppService)
		{
			_documentAppService = documentAppService;
		}

		/// <summary>
		/// 读取文档原件：默认内联预览，<c>download=true</c> 时按附件下载
		/// </summary>
		/// <param name="id">文档 ID</param>
		/// <param name="download">是否强制下载</param>
		/// <param name="cancellationToken">取消令牌</param>
		/// <returns>文件流</returns>
		[HttpGet("{id:guid}/content")]
		public async Task<IActionResult> GetContentAsync(Guid id, [FromQuery] bool download, CancellationToken cancellationToken)
		{
			try
			{
				var content = await _documentAppService.GetContentAsync(id, cancellationToken);
				return download
					? File(content.Stream, content.ContentType, content.FileName, enableRangeProcessing: true)
					: File(content.Stream, content.ContentType, enableRangeProcessing: true);
			}
			// ABP 的异常过滤器只处理对象结果，文件接口要自己映射状态码
			catch (AbpAuthorizationException)
			{
				return Forbid();
			}
			catch (EntityNotFoundException)
			{
				return NotFound();
			}
			catch (UserFriendlyException ex)
			{
				return StatusCode(StatusCodes.Status403Forbidden, new { error = new { message = ex.Message } });
			}
		}
	}
}
