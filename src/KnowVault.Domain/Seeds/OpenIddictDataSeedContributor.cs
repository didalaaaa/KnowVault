using Microsoft.Extensions.Configuration;
using OpenIddict.Abstractions;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.OpenIddict;
using Volo.Abp.OpenIddict.Applications;
using Volo.Abp.OpenIddict.Scopes;
using Volo.Abp.Uow;

namespace KnowVault.Domain
{

	/* Creates initial data that is needed to property run the application
	 * and make client-to-server communication possible.
	 */
	public class OpenIddictDataSeedContributor : OpenIddictDataSeedContributorBase, IDataSeedContributor, ITransientDependency
	{
		public OpenIddictDataSeedContributor(
			IConfiguration configuration,
			IOpenIddictApplicationRepository openIddictApplicationRepository,
			IAbpApplicationManager applicationManager,
			IOpenIddictScopeRepository openIddictScopeRepository,
			IOpenIddictScopeManager scopeManager)
			: base(configuration, openIddictApplicationRepository, applicationManager, openIddictScopeRepository, scopeManager)
		{
		}

		[UnitOfWork]
		public virtual async Task SeedAsync(DataSeedContext context)
		{
			await CreateScopesAsync();
			await CreateApplicationsAsync();
		}

		private async Task CreateScopesAsync()
		{
			await CreateScopesAsync(new OpenIddictScopeDescriptor
			{
				Name = "KnowVault",
				DisplayName = "KnowVault API",
				Resources = { "KnowVault" }
			});
		}

		private async Task CreateApplicationsAsync()
		{
			var commonScopes = new List<string> {
				OpenIddictConstants.Permissions.Scopes.Address,
				OpenIddictConstants.Permissions.Scopes.Email,
				OpenIddictConstants.Permissions.Scopes.Phone,
				OpenIddictConstants.Permissions.Scopes.Profile,
				OpenIddictConstants.Permissions.Scopes.Roles,
				"KnowVault"
			};

			var configurationSection = Configuration.GetSection("OpenIddict:Applications");


			// Console Test / Angular Client（同时作为 Vue SPA 的密码模式客户端）
			var appClientId = configurationSection["KnowVault_App:ClientId"];
			if (!appClientId.IsNullOrWhiteSpace())
			{
				// RootUrl 未配置时不能把 null 塞进 redirectUris：OpenIddict 会把它当非法绝对 URI 处理，
				// 导致种子失败、KnowVault_App 客户端缺失（密码模式登录必然 401）
				var appClientRootUrl = configurationSection["KnowVault_App:RootUrl"]?.TrimEnd('/');
				var appRedirectUris = appClientRootUrl.IsNullOrWhiteSpace()
					? new List<string>()
					: new List<string> { appClientRootUrl! };

				await CreateOrUpdateApplicationAsync(
					applicationType: OpenIddictConstants.ApplicationTypes.Web,
					name: appClientId!,
					type: OpenIddictConstants.ClientTypes.Public,
					consentType: OpenIddictConstants.ConsentTypes.Implicit,
					displayName: "Console Test / Angular Application",
					secret: null,
					grantTypes: new List<string> {
						OpenIddictConstants.GrantTypes.AuthorizationCode,
						OpenIddictConstants.GrantTypes.Password,
						OpenIddictConstants.GrantTypes.ClientCredentials,
						OpenIddictConstants.GrantTypes.RefreshToken,
						"LinkLogin",
						"Impersonation"
					},
					scopes: commonScopes,
					redirectUris: appRedirectUris,
					postLogoutRedirectUris: appRedirectUris,
					clientUri: appClientRootUrl,
					logoUri: "/images/clients/angular.svg"
				);
			}







			// Swagger Client
			var swaggerClientId = configurationSection["KnowVault_Swagger:ClientId"];
			var swaggerRootUrl = configurationSection["KnowVault_Swagger:RootUrl"]?.TrimEnd('/');
			if (!swaggerClientId.IsNullOrWhiteSpace() && !swaggerRootUrl.IsNullOrWhiteSpace())
			{
				await CreateOrUpdateApplicationAsync(
					applicationType: OpenIddictConstants.ApplicationTypes.Web,
					name: swaggerClientId!,
					type: OpenIddictConstants.ClientTypes.Public,
					consentType: OpenIddictConstants.ConsentTypes.Implicit,
					displayName: "Swagger Application",
					secret: null,
					grantTypes: new List<string> { OpenIddictConstants.GrantTypes.AuthorizationCode, },
					scopes: commonScopes,
					redirectUris: new List<string> { $"{swaggerRootUrl}/swagger/oauth2-redirect.html" },
					clientUri: swaggerRootUrl!.EnsureEndsWith('/') + "swagger",
					logoUri: "/images/clients/swagger.svg"
				);
			}


		}
	}

}
