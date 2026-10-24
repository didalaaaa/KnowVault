using System.ComponentModel.DataAnnotations;

namespace KnowVault.Application.Contracts
{
	/// <summary>
	/// 注册入参
	/// </summary>
	public class RegisterInput
	{
		/// <summary>
		/// 用户名（3–32 位）
		/// </summary>
		[Required]
		[StringLength(32, MinimumLength = 3)]
		public string UserName { get; set; } = string.Empty;

		/// <summary>
		/// 邮箱
		/// </summary>
		[Required]
		[EmailAddress]
		[StringLength(256)]
		public string Email { get; set; } = string.Empty;

		/// <summary>
		/// 密码（遵循项目 Identity 密码策略：至少 6 位，含大小写字母、数字与特殊字符）
		/// </summary>
		[Required]
		[StringLength(64, MinimumLength = 6)]
		public string Password { get; set; } = string.Empty;

		/// <summary>
		/// 显示名，为空时使用用户名
		/// </summary>
		[StringLength(64)]
		public string? DisplayName { get; set; }
	}
}
