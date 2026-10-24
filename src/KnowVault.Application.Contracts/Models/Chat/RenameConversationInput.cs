using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace KnowVault.Application.Contracts
{
	/// <summary>
	/// 重命名会话入参
	/// </summary>
	public class RenameConversationInput : EntityDto<Guid>
	{
		/// <summary>
		/// 新标题，裁剪首尾空白后 1–256 个字符
		/// </summary>
		[Required]
		[StringLength(256)]
		public string Title { get; set; } = string.Empty;
	}
}
