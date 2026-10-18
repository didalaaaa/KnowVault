namespace KnowVault.Domain.Shared
{
	/// <summary>
	/// KnowVault 的权限名称常量
	/// </summary>
	public static class KnowVaultPermissions
	{
		/// <summary>
		/// 权限分组名
		/// </summary>
		public const string GroupName = "KnowVault";

		/// <summary>
		/// 知识库相关权限
		/// </summary>
		public static class KnowledgeBases
		{
			/// <summary>
			/// 创建知识库
			/// </summary>
			public const string Create = GroupName + ".KnowledgeBases.Create";

			/// <summary>
			/// 管理任意知识库（管理员短路权限）
			/// </summary>
			public const string Manage = GroupName + ".KnowledgeBases.Manage";

			/// <summary>
			/// 管理知识库成员
			/// </summary>
			public const string ManageMembers = GroupName + ".KnowledgeBases.ManageMembers";
		}

		/// <summary>
		/// 文档相关权限
		/// </summary>
		public static class Documents
		{
			/// <summary>
			/// 上传文档
			/// </summary>
			public const string Upload = GroupName + ".Documents.Upload";

			/// <summary>
			/// 删除文档
			/// </summary>
			public const string Delete = GroupName + ".Documents.Delete";

			/// <summary>
			/// 重新处理文档
			/// </summary>
			public const string Reprocess = GroupName + ".Documents.Reprocess";
		}

		/// <summary>
		/// 问答相关权限
		/// </summary>
		public static class Qa
		{
			/// <summary>
			/// 发起提问
			/// </summary>
			public const string Ask = GroupName + ".Qa.Ask";
		}
	}
}
