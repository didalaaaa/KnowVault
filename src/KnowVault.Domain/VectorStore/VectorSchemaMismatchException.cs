namespace KnowVault.Domain
{
	/// <summary>
	/// 向量库 schema 与当前版本不匹配（升级后需要重建 collection）
	/// </summary>
	public class VectorSchemaMismatchException : Exception
	{
		/// <summary>
		/// 构造函数
		/// </summary>
		/// <param name="message">包含重建入口的错误信息</param>
		public VectorSchemaMismatchException(string message) : base(message)
		{
		}
	}
}
