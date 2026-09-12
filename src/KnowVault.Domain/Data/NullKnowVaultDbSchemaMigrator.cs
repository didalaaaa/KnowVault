using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace KnowVault.Domain.Data
{

	/* This is used if database provider does't define
	 * IKnowVaultDbSchemaMigrator implementation.
	 */
	public class NullKnowVaultDbSchemaMigrator : IKnowVaultDbSchemaMigrator, ITransientDependency
	{
	    public Task MigrateAsync()
	    {
	        return Task.CompletedTask;
	    }
	}

}
