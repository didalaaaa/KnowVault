using System.ComponentModel.DataAnnotations;
using Volo.Abp.Content;

namespace KnowVault.Application.Contracts
{
	/// <summary>上传文档入参（multipart/form-data，由 ABP 动态 API 绑定）。</summary>
	public class UploadDocumentInput
	{
		/// <summary>目标知识库。</summary>
		[Required]
		public Guid KnowledgeBaseId { get; set; }

		/// <summary>可选标题；为空则取文件名（不含扩展名）。</summary>
		[StringLength(256)]
		public string? Title { get; set; }

		/// <summary>上传的文件流（<see cref="IRemoteStreamContent"/> 定义在 Volo.Abp.Core）。</summary>
		[Required]
		public IRemoteStreamContent File { get; set; } = default!;
	}
}
