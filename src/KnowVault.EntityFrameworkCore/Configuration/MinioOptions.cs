namespace KnowVault.EntityFrameworkCore
{
	/// <summary>MinIO 连接参数。密钥一律来自环境变量/AppHost 注入，不在代码里写默认值。</summary>
	public class MinioOptions
	{
		/// <summary>形如 <c>localhost:9000</c>（不带协议）。</summary>
		public string Endpoint { get; set; } = string.Empty;

		/// <summary>访问密钥（由 AppHost 注入）。</summary>
		public string AccessKey { get; set; } = string.Empty;

		/// <summary>私密密钥（由 AppHost 注入）。</summary>
		public string SecretKey { get; set; } = string.Empty;

		/// <summary>是否启用 TLS；本地开发为 false（容器未启 TLS）。</summary>
		public bool UseSsl { get; set; }

		/// <summary>存放原始文档的 bucket，代码启动时会幂等创建。</summary>
		public string BucketName { get; set; } = "knowvault";
	}
}
