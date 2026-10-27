using KnowVault.Application.Contracts;
using KnowVault.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Identity;

namespace KnowVault.Application
{
	/// <summary>
	/// 账户应用服务：注册、资料与改密，底层走 <see cref="IdentityUserManager"/>
	/// </summary>
	[Authorize]
	public class AccountAppService : KnowVaultAppService, IAccountAppService
	{
		private readonly IdentityUserManager _userManager;
		private readonly IIdentityUserRepository _userRepository;

		/// <summary>
		/// 构造函数
		/// </summary>
		/// <param name="userManager">Identity 用户管理器</param>
		/// <param name="userRepository">Identity 用户仓储</param>
		public AccountAppService(
			IdentityUserManager userManager,
			IIdentityUserRepository userRepository)
		{
			_userManager = userManager;
			_userRepository = userRepository;
		}

		/// <inheritdoc />
		[AllowAnonymous]
		public async Task<AccountProfileDto> RegisterAsync(RegisterInput input, CancellationToken cancellationToken)
		{
			var userName = input.UserName.Trim();
			var email = input.Email.Trim();

			if (await _userRepository.FindByNormalizedUserNameAsync(userName.ToUpperInvariant(), cancellationToken: cancellationToken) is not null)
			{
				throw new UserFriendlyException($"用户名「{userName}」已被注册，请换一个");
			}

			if (await _userRepository.FindByNormalizedEmailAsync(email.ToUpperInvariant(), cancellationToken: cancellationToken) is not null)
			{
				throw new UserFriendlyException($"邮箱「{email}」已被注册，请换一个");
			}

			var user = new IdentityUser(GuidGenerator.Create(), userName, email)
			{
				Name = string.IsNullOrWhiteSpace(input.DisplayName) ? userName : input.DisplayName.Trim()
			};

			EnsureSucceeded(await ExecuteAsync(() => _userManager.CreateAsync(user, input.Password)), "注册失败");

			var roleResult = await _userManager.AddToRoleAsync(user, KnowVaultIdentityDataSeedContributor.MemberRoleName);
			EnsureSucceeded(roleResult, "分配默认角色失败");

			return new AccountProfileDto
			{
				Id = user.Id,
				UserName = user.UserName ?? userName,
				DisplayName = user.Name,
				Email = user.Email,
				Roles = [KnowVaultIdentityDataSeedContributor.MemberRoleName]
			};
		}

		/// <inheritdoc />
		public async Task<AccountProfileDto> GetProfileAsync(CancellationToken cancellationToken)
		{
			var user = await GetCurrentUserAsync(cancellationToken);
			return MapToProfile(user, await _userManager.GetRolesAsync(user));
		}

		/// <inheritdoc />
		public async Task<AccountProfileDto> UpdateProfileAsync(UpdateProfileInput input, CancellationToken cancellationToken)
		{
			var user = await GetCurrentUserAsync(cancellationToken);
			var email = input.Email.Trim();

			var owner = await _userRepository.FindByNormalizedEmailAsync(email.ToUpperInvariant(), cancellationToken: cancellationToken);
			if (owner is not null && owner.Id != user.Id)
			{
				throw new UserFriendlyException($"邮箱「{email}」已被其他账号使用");
			}

			user.Name = string.IsNullOrWhiteSpace(input.DisplayName) ? user.UserName : input.DisplayName.Trim();

			if (!string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))
			{
				EnsureSucceeded(await ExecuteAsync(() => _userManager.SetEmailAsync(user, email)), "修改邮箱失败");
			}
			else
			{
				EnsureSucceeded(await ExecuteAsync(() => _userManager.UpdateAsync(user)), "修改资料失败");
			}

			return MapToProfile(user, await _userManager.GetRolesAsync(user));
		}

		/// <inheritdoc />
		public async Task ChangePasswordAsync(ChangePasswordInput input, CancellationToken cancellationToken)
		{
			var user = await GetCurrentUserAsync(cancellationToken);

			EnsureSucceeded(
				await ExecuteAsync(() => _userManager.ChangePasswordAsync(user, input.CurrentPassword, input.NewPassword)),
				"修改密码失败");
		}

		private async Task<IdentityUser> GetCurrentUserAsync(CancellationToken cancellationToken)
		{
			if (CurrentUser.Id is null)
			{
				throw new AbpAuthorizationException("请先登录");
			}

			return await _userRepository.GetAsync(CurrentUser.Id.Value, cancellationToken: cancellationToken);
		}

		private static AccountProfileDto MapToProfile(IdentityUser user, IEnumerable<string> roles) => new()
		{
			Id = user.Id,
			UserName = user.UserName ?? string.Empty,
			DisplayName = user.Name,
			Email = user.Email,
			Roles = [.. roles]
		};

		private static void EnsureSucceeded(IdentityResult result, string action)
		{
			if (result.Succeeded) return;

			throw new UserFriendlyException($"{action}：{string.Join("；", result.Errors.Select(TranslateError))}");
		}

		/// <summary>
		/// 统一 Identity 失败形态：ABP 的 <see cref="IdentityUserManager"/> 部分方法（改密、更新）
		/// 直接抛 <see cref="AbpIdentityResultException"/>，这里还原成 <see cref="IdentityResult"/>，
		/// 与返回失败结果的方法走同一套中文提示
		/// </summary>
		/// <param name="action">Identity 调用</param>
		/// <returns>Identity 结果</returns>
		private static async Task<IdentityResult> ExecuteAsync(Func<Task<IdentityResult>> action)
		{
			try
			{
				return await action();
			}
			catch (AbpIdentityResultException ex)
			{
				return ex.IdentityResult;
			}
		}

		private static string TranslateError(IdentityError error) => error.Code switch
		{
			nameof(IdentityErrorDescriber.DuplicateUserName) => "用户名已存在",
			nameof(IdentityErrorDescriber.DuplicateEmail) => "邮箱已被使用",
			nameof(IdentityErrorDescriber.InvalidUserName) => "用户名只能包含字母、数字及 . _ @ + -",
			nameof(IdentityErrorDescriber.PasswordTooShort) => "密码长度不足（至少 6 位）",
			nameof(IdentityErrorDescriber.PasswordRequiresDigit) => "密码需包含数字",
			nameof(IdentityErrorDescriber.PasswordRequiresLower) => "密码需包含小写字母",
			nameof(IdentityErrorDescriber.PasswordRequiresUpper) => "密码需包含大写字母",
			nameof(IdentityErrorDescriber.PasswordRequiresNonAlphanumeric) => "密码需包含特殊字符",
			nameof(IdentityErrorDescriber.PasswordRequiresUniqueChars) => "密码中重复字符过多",
			nameof(IdentityErrorDescriber.PasswordMismatch) => "当前密码不正确",
			_ => error.Description
		};
	}
}
