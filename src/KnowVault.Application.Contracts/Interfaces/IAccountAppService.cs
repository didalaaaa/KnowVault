using Volo.Abp.Application.Services;

namespace KnowVault.Application.Contracts
{
	/// <summary>
	/// 账户应用服务：注册、资料、改密
	/// </summary>
	public interface IAccountAppService : IApplicationService
	{
		/// <summary>
		/// 注册新用户并加入 Member 角色
		/// </summary>
		/// <param name="input">注册信息</param>
		/// <param name="cancellationToken">取消令牌</param>
		/// <returns>新用户资料</returns>
		Task<AccountProfileDto> RegisterAsync(RegisterInput input, CancellationToken cancellationToken);

		/// <summary>
		/// 获取当前登录用户资料
		/// </summary>
		/// <param name="cancellationToken">取消令牌</param>
		/// <returns>当前用户资料</returns>
		Task<AccountProfileDto> GetProfileAsync(CancellationToken cancellationToken);

		/// <summary>
		/// 修改当前用户的显示名与邮箱
		/// </summary>
		/// <param name="input">资料入参</param>
		/// <param name="cancellationToken">取消令牌</param>
		/// <returns>更新后的资料</returns>
		Task<AccountProfileDto> UpdateProfileAsync(UpdateProfileInput input, CancellationToken cancellationToken);

		/// <summary>
		/// 修改当前用户密码
		/// </summary>
		/// <param name="input">原密码与新密码</param>
		/// <param name="cancellationToken">取消令牌</param>
		/// <returns></returns>
		Task ChangePasswordAsync(ChangePasswordInput input, CancellationToken cancellationToken);
	}
}
