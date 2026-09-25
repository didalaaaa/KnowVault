using KnowVault.Domain.Shared;
using Volo.Abp.AspNetCore.Mvc;

namespace KnowVault.HttpApi
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
