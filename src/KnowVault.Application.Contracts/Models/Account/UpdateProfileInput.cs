using System.ComponentModel.DataAnnotations;

namespace KnowVault.Application.Contracts
{
	/// <summary>
	/// 修改资料入参
	/// </summary>
	public class UpdateProfileInput
	{
		/// <summary>
		/// 显示名，为空时回落用户名
		/// </summary>
		[StringLength(64)]
		public string? DisplayName { get; set; }

		/// <summary>
		/// 邮箱
		/// </summary>
		[Required]
		[EmailAddress]
		[StringLength(256)]
		public string Email { get; set; } = string.Empty;
	}
}
