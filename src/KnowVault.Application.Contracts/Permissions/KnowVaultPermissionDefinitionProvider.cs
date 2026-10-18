using KnowVault.Domain.Shared;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;

namespace KnowVault.Application.Contracts
{
	/// <summary>
	/// 注册 KnowVault 的权限定义；显示名直接用中文，避免再补一套本地化资源
	/// </summary>
	public class KnowVaultPermissionDefinitionProvider : PermissionDefinitionProvider
	{
		/// <summary>
		/// 定义权限分组与权限项
		/// </summary>
		/// <param name="context">权限定义上下文</param>
		public override void Define(IPermissionDefinitionContext context)
		{
			var group = context.AddGroup(KnowVaultPermissions.GroupName);

			group.AddPermission(KnowVaultPermissions.KnowledgeBases.Create, Fixed("创建知识库"));
			group.AddPermission(KnowVaultPermissions.KnowledgeBases.Manage, Fixed("管理全部知识库"));
			group.AddPermission(KnowVaultPermissions.KnowledgeBases.ManageMembers, Fixed("管理知识库成员"));
			group.AddPermission(KnowVaultPermissions.Documents.Upload, Fixed("上传文档"));
			group.AddPermission(KnowVaultPermissions.Documents.Delete, Fixed("删除文档"));
			group.AddPermission(KnowVaultPermissions.Documents.Reprocess, Fixed("重新处理文档"));
			group.AddPermission(KnowVaultPermissions.Qa.Ask, Fixed("知识库提问"));
		}

		private static ILocalizableString Fixed(string text) => new FixedLocalizableString(text);
	}
}
