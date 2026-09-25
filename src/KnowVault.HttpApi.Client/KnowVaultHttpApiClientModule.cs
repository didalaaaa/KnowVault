using Microsoft.Extensions.DependencyInjection;
using KnowVault.Application.Contracts;
using Volo.Abp.Account;
using Volo.Abp.Modularity;
using Volo.Abp.PermissionManagement;
using Volo.Abp.SettingManagement;
using Volo.Abp.VirtualFileSystem;
using Volo.Abp.FeatureManagement;
using Volo.Abp.Identity;
using Volo.Abp.TenantManagement;

namespace KnowVault.HttpApi.Client
{
	[DependsOn(
		typeof(KnowVaultApplicationContractsModule),
		typeof(AbpPermissionManagementHttpApiClientModule),
		typeof(AbpFeatureManagementHttpApiClientModule),
		typeof(AbpAccountHttpApiClientModule),
		typeof(AbpIdentityHttpApiClientModule),
		typeof(AbpTenantManagementHttpApiClientModule),
		typeof(AbpSettingManagementHttpApiClientModule)
	)]
	public class KnowVaultHttpApiClientModule : AbpModule
	{
		public const string RemoteServiceName = "Default";

		public override void ConfigureServices(ServiceConfigurationContext context)
		{
			// 注册Http客户端代理
			// ABP 提供了一种机制：让你像调用本地接口一样调用远程 HTTP API，无需手写 HttpClient 请求代码
			context.Services.AddHttpClientProxies(
				typeof(KnowVaultApplicationContractsModule).Assembly,
				RemoteServiceName
			);

			Configure<AbpVirtualFileSystemOptions>(options =>
			{
				// 把 KnowVaultHttpApiClientModule 所在程序集中的嵌入式资源注册到 ABP 虚拟文件系统；文件包括：本地化文件（Localization/en.json、zh-Hans.json）
				options.FileSets.AddEmbedded<KnowVaultHttpApiClientModule>();
			});
		}
	}
}
