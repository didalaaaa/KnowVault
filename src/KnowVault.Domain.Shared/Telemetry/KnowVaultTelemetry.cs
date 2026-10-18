namespace KnowVault.Domain.Shared
{
	/// <summary>
	/// 全链路追踪的统一 ActivitySource 名称
	/// </summary>
	public static class KnowVaultTelemetry
	{
		/// <summary>
		/// 自定义 span 的 ActivitySource 名称
		/// </summary>
		public const string ActivitySourceName = "KnowVault";

		/// <summary>
		/// Seq 中显示的服务名
		/// </summary>
		public const string ServiceName = "KnowVault";
	}
}
