using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace KnowVault.HttpApi.Host
{
	public static class HealthChecksBuilderExtensions
	{
		public static void AddKnowVaultHealthChecks(this IServiceCollection services)
		{
			// Add your health checks here
			var healthChecksBuilder = services.AddHealthChecks();
			// 添加数据库健康检查
			healthChecksBuilder.AddCheck<KnowVaultDatabaseCheck>("KnowVault DbContext Check", tags: new string[] { "database" });

			services.ConfigureHealthCheckEndpoint("/health-status");

			var configuration = services.GetConfiguration();
			// 读取要监控的URL
			var healthCheckUrl = configuration["App:HealthCheckUrl"];

			if (string.IsNullOrEmpty(healthCheckUrl))
			{
				healthCheckUrl = "/health-status";
			}

			var healthChecksUiBuilder = services.AddHealthChecksUI(settings =>
			{
				// 被监控应用的端点地址（UI后台调度使用）
				settings.AddHealthCheckEndpoint("KnowVault Health Status", configuration["App:HealthUiCheckUrl"] ?? healthCheckUrl);
			});

			// Set your HealthCheck UI Storage here
			healthChecksUiBuilder.AddInMemoryStorage();

			services.MapHealthChecksUiEndpoints(options =>
			{
				// UI监控面板访问地址
				options.UIPath = "/health-ui";
				// UI监控面板接口地址
				options.ApiPath = "/health-api";
			});
		}

		private static IServiceCollection ConfigureHealthCheckEndpoint(this IServiceCollection services, string path)
		{
			services.Configure<AbpEndpointRouterOptions>(options =>
			{
				options.EndpointConfigureActions.Add(endpointContext =>
				{
					endpointContext.Endpoints.MapHealthChecks(
						new PathString(path.EnsureStartsWith('/')),
						new HealthCheckOptions
						{
							// 执行所有健康检查（不筛选）；限制筛选 r => r.Tags.Contains("database")
							Predicate = _ => true,
							// 用 HealthChecks UI 的格式输出 JSON，而不是默认的纯文本 Healthy
							ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse,
							// 禁止缓存响应
							AllowCachingResponses = false,
						});
				});
			});

			return services;
		}

		private static IServiceCollection MapHealthChecksUiEndpoints(this IServiceCollection services, Action<global::HealthChecks.UI.Configuration.Options>? setupOption = null)
		{
			services.Configure<AbpEndpointRouterOptions>(routerOptions =>
			{
				routerOptions.EndpointConfigureActions.Add(endpointContext =>
				{
					// 注册UI相关端点
					endpointContext.Endpoints.MapHealthChecksUI(setupOption);
				});
			});

			return services;
		}
	}
}
