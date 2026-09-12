using Volo.Abp.Settings;

namespace KnowVault.Domain.Settings
{

	public class KnowVaultSettingDefinitionProvider : SettingDefinitionProvider
	{
	    public override void Define(ISettingDefinitionContext context)
	    {
	        //Define your own settings here. Example:
	        //context.Add(new SettingDefinition(KnowVaultSettings.MySetting1));
	    }
	}

}
