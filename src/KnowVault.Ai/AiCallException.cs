namespace KnowVault.Ai
{
	/// <summary>调用外部模型服务失败（HTTP 非 2xx、响应结构异常等）。</summary>
	public class AiCallException : Exception
	{
		/// <summary>用错误信息构造。</summary>
		/// <param name="message">错误详情（含 HTTP 状态码与服务端返回体，便于排查）。</param>
		public AiCallException(string message) : base(message)
		{
		}

		/// <summary>用错误信息与内部异常构造。</summary>
		/// <param name="message">错误详情。</param>
		/// <param name="innerException">内部异常。</param>
		public AiCallException(string message, Exception innerException) : base(message, innerException)
		{
		}
	}
}
