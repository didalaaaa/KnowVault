using KnowVault.Domain.Shared.Localization;
using Volo.Abp.Application.Services;

namespace KnowVault.Application
{

	/* Inherit your application services from this class.
	 */
	public abstract class KnowVaultAppService : ApplicationService
	{
	    protected KnowVaultAppService()
	    {
	        LocalizationResource = typeof(KnowVaultResource);
	    }
	}

}
