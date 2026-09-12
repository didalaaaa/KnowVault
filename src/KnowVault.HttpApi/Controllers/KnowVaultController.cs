using KnowVault.Domain.Shared.Localization;
using Volo.Abp.AspNetCore.Mvc;

namespace KnowVault.HttpApi.Controllers
{

	/* Inherit your controllers from this class.
	 */
	public abstract class KnowVaultController : AbpControllerBase
	{
	    protected KnowVaultController()
	    {
	        LocalizationResource = typeof(KnowVaultResource);
	    }
	}

}
