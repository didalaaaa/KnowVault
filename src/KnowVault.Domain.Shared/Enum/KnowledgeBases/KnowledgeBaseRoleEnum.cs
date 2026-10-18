namespace KnowVault.Domain.Shared
{
	/// <summary>
	/// 知识库成员角色；数值越大权限越高
	/// </summary>
	public enum KnowledgeBaseRoleEnum
	{
		/// <summary>
		/// 只读：可查看知识库、文档与提问
		/// </summary>
		Viewer = 1,

		/// <summary>
		/// 编辑：可上传、删除、重新处理文档
		/// </summary>
		Editor = 2,

		/// <summary>
		/// 所有者：可修改/删除知识库并管理成员
		/// </summary>
		Owner = 3
	}
}
