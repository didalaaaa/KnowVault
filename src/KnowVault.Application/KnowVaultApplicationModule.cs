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
		public override void ConfigureServices(ServiceConfigurationContext context)
		{
			var configuration = context.Services.GetConfiguration();

			// 上传约束（白名单 / 大小上限）：应用层校验用，Ingest 只关心切片参数
			Configure<DocumentUploadOptions>(configuration.GetSection("Documents"));
		}
	}

}
