using KnowVault.Domain.Shared;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace KnowVault.HttpApi.Host
{
	/// <summary>
	/// OpenTelemetry 注册入口：trace 走 OTLP/HTTP 进 Seq，指标暂不导出
	/// </summary>
	public static class KnowVaultObservabilityExtensions
	{
		/// <summary>
		/// 注册 ASP.NET Core、HttpClient、EF Core 与自定义 ActivitySource 的链路追踪
		/// </summary>
		/// <param name="services">服务集合</param>
		/// <param name="configuration">配置（读取 Observability:OtlpEndpoint）</param>
		/// <returns>服务集合，便于链式调用</returns>
		public static IServiceCollection AddKnowVaultObservability(this IServiceCollection services, IConfiguration configuration)
		{
			var endpoint = configuration["Observability:OtlpEndpoint"];

			services.AddOpenTelemetry()
				.ConfigureResource(resource => resource.AddService(KnowVaultTelemetry.ServiceName))
				.WithTracing(tracing =>
				{
					tracing
						.AddSource(KnowVaultTelemetry.ActivitySourceName)
						.AddAspNetCoreInstrumentation(options =>
						{
							// 健康检查探测频率高，纳入 trace 只会淹没真正的请求
							options.Filter = context => !context.Request.Path.StartsWithSegments("/health");
						})
						.AddHttpClientInstrumentation()
						.AddEntityFrameworkCoreInstrumentation();

					// Seq 的 OTLP 只支持 HTTP/protobuf；端点必须是完整的 /ingest/otlp/v1/traces
					if (!string.IsNullOrWhiteSpace(endpoint))
					{
						tracing.AddOtlpExporter(options =>
						{
							options.Endpoint = new Uri(endpoint);
							options.Protocol = OtlpExportProtocol.HttpProtobuf;
						});
					}
				});

			return services;
		}
	}
}
