using System.Threading.Tasks;

namespace KnowVault.Domain.Data
{

	public interface IKnowVaultDbSchemaMigrator
	{
	    Task MigrateAsync();
	}

}
