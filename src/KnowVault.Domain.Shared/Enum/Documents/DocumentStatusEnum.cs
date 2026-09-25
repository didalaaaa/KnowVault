namespace KnowVault.Domain.Shared
{

	/// <summary>
	/// 文档处理状态机：Pending -> Processing -> Ready / Failed。
	/// </summary>
	public enum DocumentStatusEnum
	{
		/// <summary>已提交，等待异步处理</summary>
		Pending = 0,

		/// <summary>解析/切片/向量化处理中</summary>
		Processing = 1,

		/// <summary>处理完成，可被检索</summary>
		Ready = 2,

		/// <summary>处理失败（错误见 ErrorMessage）</summary>
		Failed = 3
	}

}
