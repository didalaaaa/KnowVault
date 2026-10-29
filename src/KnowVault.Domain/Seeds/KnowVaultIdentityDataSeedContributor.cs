using KnowVault.Domain.Shared;
using Microsoft.Extensions.Configuration;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Identity;
using Volo.Abp.PermissionManagement;

namespace KnowVault.Domain
{
	/// <summary>
	/// 初始化 P3 的系统角色（Admin / Member / Viewer）及其权限，并把内置 admin 用户加入 Admin 角色
	/// </summary>
	public class KnowVaultIdentityDataSeedContributor : IDataSeedContributor, ITransientDependency
	{
		/// <summary>
		/// 管理员角色名
		/// </summary>
		public const string AdminRoleName = "Admin";

		/// <summary>
		/// 普通成员角色名
		/// </summary>
		public const string MemberRoleName = "Member";

		/// <summary>
		/// 只读成员角色名
		/// </summary>
		public const string ViewerRoleName = "Viewer";

		private static readonly string[] AllPermissions =
		[
			KnowVaultPermissions.KnowledgeBases.Create,
			KnowVaultPermissions.KnowledgeBases.Manage,
			KnowVaultPermissions.KnowledgeBases.ManageMembers,
			KnowVaultPermissions.Documents.Upload,
			KnowVaultPermissions.Documents.Delete,
			KnowVaultPermissions.Documents.Reprocess,
			KnowVaultPermissions.Qa.Ask
		];

		private static readonly string[] MemberPermissions =
		[
			KnowVaultPermissions.KnowledgeBases.Create,
			KnowVaultPermissions.Documents.Upload,
			KnowVaultPermissions.Documents.Delete,
			KnowVaultPermissions.Documents.Reprocess,
			KnowVaultPermissions.Qa.Ask
		];

		private static readonly string[] ViewerPermissions =
		[
			KnowVaultPermissions.Qa.Ask
		];

		private const string DemoPassword = "1q2w3E*";

		private readonly IdentityRoleManager _roleManager;
		private readonly IIdentityRoleRepository _roleRepository;
		private readonly IIdentityUserRepository _userRepository;
		private readonly IdentityUserManager _userManager;
		private readonly IPermissionManager _permissionManager;
		private readonly IConfiguration _configuration;

		/// <summary>
		/// 构造函数
		/// </summary>
		/// <param name="roleManager">角色管理器</param>
		/// <param name="roleRepository">角色仓储</param>
		/// <param name="userRepository">用户仓储</param>
		/// <param name="userManager">用户管理器</param>
		/// <param name="permissionManager">权限管理器</param>
		/// <param name="configuration">配置（Demo:SeedAccounts 开关）</param>
		public KnowVaultIdentityDataSeedContributor(
			IdentityRoleManager roleManager,
			IIdentityRoleRepository roleRepository,
			IIdentityUserRepository userRepository,
			IdentityUserManager userManager,
			IPermissionManager permissionManager,
			IConfiguration configuration)
		{
			_roleManager = roleManager;
			_roleRepository = roleRepository;
			_userRepository = userRepository;
			_userManager = userManager;
			_permissionManager = permissionManager;
			_configuration = configuration;
		}

		/// <summary>
		/// 幂等创建角色、授予权限并绑定管理员
		/// </summary>
		/// <param name="context">种子上下文</param>
		/// <returns>任务</returns>
		public async Task SeedAsync(DataSeedContext context)
		{
			await EnsureRoleAsync(AdminRoleName);
			await EnsureRoleAsync(MemberRoleName);
			await EnsureRoleAsync(ViewerRoleName);

			await GrantAsync(AdminRoleName, AllPermissions);
			await GrantAsync(MemberRoleName, MemberPermissions);
			await GrantAsync(ViewerRoleName, ViewerPermissions);

			await EnsureAdminUserInRoleAsync();

			// 演示账号由配置控制：本地开发与 Compose 打开，正式部署默认关闭
			if (_configuration.GetValue<bool>("Demo:SeedAccounts"))
			{
				await EnsureUserInRoleAsync("demo", "demo@knowvault.local", MemberRoleName);
				await EnsureUserInRoleAsync("viewer", "viewer@knowvault.local", ViewerRoleName);
			}
		}

		private async Task EnsureUserInRoleAsync(string userName, string email, string roleName)
		{
			var normalized = userName.ToUpperInvariant();
			var user = await _userRepository.FindByNormalizedUserNameAsync(normalized);
			if (user is null)
			{
				user = new IdentityUser(Guid.NewGuid(), userName, email)
				{
					Name = userName
				};

				var created = await _userManager.CreateAsync(user, DemoPassword);
				if (!created.Succeeded)
				{
					throw new AbpException($"创建演示用户 {userName} 失败：{string.Join("；", created.Errors.Select(x => x.Description))}");
				}
			}

			if (await _userManager.IsInRoleAsync(user, roleName)) return;

			var result = await _userManager.AddToRoleAsync(user, roleName);
			if (!result.Succeeded)
			{
				throw new AbpException($"把 {userName} 加入 {roleName} 失败：{string.Join("；", result.Errors.Select(x => x.Description))}");
			}
		}

		private async Task EnsureRoleAsync(string roleName)
		{
			var existing = await _roleRepository.FindByNormalizedNameAsync(roleName.ToUpperInvariant());
			if (existing is not null) return;

			var result = await _roleManager.CreateAsync(new IdentityRole(Guid.NewGuid(), roleName));
			if (!result.Succeeded)
			{
				throw new AbpException($"创建角色 {roleName} 失败：{string.Join("；", result.Errors.Select(x => x.Description))}");
			}
		}

		private async Task GrantAsync(string roleName, IEnumerable<string> permissions)
		{
			foreach (var permission in permissions)
			{
				await _permissionManager.SetForRoleAsync(roleName, permission, true);
			}
		}

		private async Task EnsureAdminUserInRoleAsync()
		{
			var admin = await _userRepository.FindByNormalizedUserNameAsync("ADMIN");
			if (admin is null)
			{
				// 首次迁移时 Identity 种子可能还没执行；下次启动会补上
				return;
			}

			if (await _userManager.IsInRoleAsync(admin, AdminRoleName)) return;

			var result = await _userManager.AddToRoleAsync(admin, AdminRoleName);
			if (!result.Succeeded)
			{
				throw new AbpException($"把 admin 加入 {AdminRoleName} 失败：{string.Join("；", result.Errors.Select(x => x.Description))}");
			}
		}
	}
}
