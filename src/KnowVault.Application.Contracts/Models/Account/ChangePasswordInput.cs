using System.ComponentModel.DataAnnotations;

namespace KnowVault.Application.Contracts
{
	/// <summary>
	/// 修改密码入参
	/// </summary>
	public class ChangePasswordInput
	{
		/// <summary>
		/// 当前密码
		/// </summary>
		[Required]
		public string CurrentPassword { get; set; } = string.Empty;

		/// <summary>
		/// 新密码（遵循 Identity 默认强度）
		/// </summary>
		[Required]
		[StringLength(64, MinimumLength = 6)]
		public string NewPassword { get; set; } = string.Empty;
	}
}
