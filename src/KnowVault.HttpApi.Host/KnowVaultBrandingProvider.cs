using Microsoft.Extensions.Localization;
using KnowVault.Domain.Shared.Localization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Ui.Branding;

namespace KnowVault.HttpApi.Host
{

	[Dependency(ReplaceServices = true)]
	public class KnowVaultBrandingProvider : DefaultBrandingProvider
	{
	    private IStringLocalizer<KnowVaultResource> _localizer;

	    public KnowVaultBrandingProvider(IStringLocalizer<KnowVaultResource> localizer)
	    {
	        _localizer = localizer;
	    }

	    public override string AppName => _localizer["AppName"];
	}

}
