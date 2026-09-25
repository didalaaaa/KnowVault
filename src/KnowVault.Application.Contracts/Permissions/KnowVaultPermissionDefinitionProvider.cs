using KnowVault.Domain.Shared;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;

namespace KnowVault.Application.Contracts
{

	public class KnowVaultPermissionDefinitionProvider : PermissionDefinitionProvider
	{
		public override void Define(IPermissionDefinitionContext context)
		{
			var myGroup = context.AddGroup(KnowVaultPermissions.GroupName);

			//Define your own permissions here. Example:
			//myGroup.AddPermission(KnowVaultPermissions.MyPermission1, L("Permission:MyPermission1"));
		}

		private static LocalizableString L(string name)
		{
			return LocalizableString.Create<KnowVaultResource>(name);
		}
	}

}
