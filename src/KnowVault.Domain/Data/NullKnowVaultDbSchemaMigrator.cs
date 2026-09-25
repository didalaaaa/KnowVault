using Volo.Abp.DependencyInjection;

namespace KnowVault.Domain
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
