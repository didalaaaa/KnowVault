using Volo.Abp.PermissionManagement;
using Volo.Abp.SettingManagement;
using Volo.Abp.Account;
using Volo.Abp.Identity;
using Volo.Abp.Mapperly;
using Volo.Abp.FeatureManagement;
using Volo.Abp.Modularity;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.TenantManagement;
using KnowVault.Ai;
using KnowVault.Application.Contracts;
using KnowVault.Domain;

namespace KnowVault.Application
{

	[DependsOn(
	    typeof(KnowVaultDomainModule),
	    typeof(KnowVaultApplicationContractsModule),
	    typeof(KnowVaultAiModule),
	    typeof(AbpPermissionManagementApplicationModule),
	    typeof(AbpFeatureManagementApplicationModule),
	    typeof(AbpIdentityApplicationModule),
	    typeof(AbpAccountApplicationModule),
	    typeof(AbpTenantManagementApplicationModule),
	    typeof(AbpSettingManagementApplicationModule)
	    )]
	public class KnowVaultApplicationModule : AbpModule
	{

	}

}
