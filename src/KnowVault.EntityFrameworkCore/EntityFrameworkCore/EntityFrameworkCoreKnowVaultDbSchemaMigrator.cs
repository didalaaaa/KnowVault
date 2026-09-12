using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using KnowVault.Domain.Data;
using Volo.Abp.DependencyInjection;

namespace KnowVault.EntityFrameworkCore
{

	public class EntityFrameworkCoreKnowVaultDbSchemaMigrator
	    : IKnowVaultDbSchemaMigrator, ITransientDependency
	{
	    private readonly IServiceProvider _serviceProvider;

	    public EntityFrameworkCoreKnowVaultDbSchemaMigrator(IServiceProvider serviceProvider)
	    {
	        _serviceProvider = serviceProvider;
	    }

	    public async Task MigrateAsync()
	    {
	        /* We intentionally resolving the KnowVaultDbContext
	         * from IServiceProvider (instead of directly injecting it)
	         * to properly get the connection string of the current tenant in the
	         * current scope.
	         */

	        await _serviceProvider
	            .GetRequiredService<KnowVaultDbContext>()
	            .Database
	            .MigrateAsync();
	    }
	}

}
