namespace KnowVault.Domain
{
	public interface IKnowVaultDbSchemaMigrator
	{
		Task MigrateAsync();
	}
}
