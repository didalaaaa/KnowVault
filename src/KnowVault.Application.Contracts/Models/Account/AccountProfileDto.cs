namespace KnowVault.Application.Contracts
{
	/// <summary>
	/// 当前用户资料
	/// </summary>
	public class AccountProfileDto
	{
		/// <summary>
		/// 用户主键
		/// </summary>
		public Guid Id { get; set; }

		/// <summary>
		/// 登录用户名
		/// </summary>
		public string UserName { get; set; } = string.Empty;

		/// <summary>
		/// 显示名
		/// </summary>
		public string? DisplayName { get; set; }

		/// <summary>
		/// 邮箱
		/// </summary>
		public string? Email { get; set; }

		/// <summary>
		/// 所属角色（Admin / Member / Viewer）
		/// </summary>
		public IReadOnlyList<string> Roles { get; set; } = [];
	}
}
