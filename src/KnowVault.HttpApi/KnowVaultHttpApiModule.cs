using KnowVault.Application.Contracts;
using KnowVault.Domain.Shared;
using Localization.Resources.AbpUi;
using Volo.Abp.Account;
using Volo.Abp.FeatureManagement;
using Volo.Abp.Identity;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;
using Volo.Abp.PermissionManagement.HttpApi;
using Volo.Abp.SettingManagement;
using Volo.Abp.TenantManagement;

namespace KnowVault.HttpApi
{
	[DependsOn(
	   typeof(KnowVaultApplicationContractsModule),
	   typeof(AbpPermissionManagementHttpApiModule),
	   typeof(AbpSettingManagementHttpApiModule),
	   typeof(AbpAccountHttpApiModule),
	   typeof(AbpIdentityHttpApiModule),
	   typeof(AbpTenantManagementHttpApiModule),
	   typeof(AbpFeatureManagementHttpApiModule)
	   )]
	public class KnowVaultHttpApiModule : AbpModule
	{
		public override void ConfigureServices(ServiceConfigurationContext context)
		{
			ConfigureLocalization();
		}

		private void ConfigureLocalization()
		{
			Configure<AbpLocalizationOptions>(options =>
			{
				// 加载本地化资源；如：多语言包
				options.Resources
					.Get<KnowVaultResource>()
					.AddBaseTypes(
						typeof(AbpUiResource)
					);
			});
		}
	}
}
