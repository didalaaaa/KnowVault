using KnowVault.Application.Contracts;
using KnowVault.EntityFrameworkCore;
using Volo.Abp.Autofac;
using Volo.Abp.Modularity;

namespace KnowVault.DbMigrator
{

	[DependsOn(
	    typeof(AbpAutofacModule),
	    typeof(KnowVaultEntityFrameworkCoreModule),
	    typeof(KnowVaultApplicationContractsModule)
	)]
	public class KnowVaultDbMigratorModule : AbpModule
	{
	}

}
