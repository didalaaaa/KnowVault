using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace KnowVault.DbMigrator
{

	class Program
	{
		static async Task Main(string[] args)
		{
			Log.Logger = new LoggerConfiguration()
				.MinimumLevel.Information()
				.MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
				.MinimumLevel.Override("Volo.Abp", LogEventLevel.Warning)
#if DEBUG
					.MinimumLevel.Override("KnowVault", LogEventLevel.Debug)
#else
	                .MinimumLevel.Override("KnowVault", LogEventLevel.Information)
#endif
					.Enrich.FromLogContext() // 从日志上下文中提取属性（如 UserId、TenantId）加入日志
				.WriteTo.Async(c => c.File("Logs/logs.txt"))
				.WriteTo.Async(c => c.Console())
				.CreateLogger();

			await CreateHostBuilder(args).RunConsoleAsync();
		}

		public static IHostBuilder CreateHostBuilder(string[] args)
		{
			return Host.CreateDefaultBuilder(args) // .NET 通用主机构建器，默认加载配置、控制台日志等
				// 关键：DbMigrator 没有 launchSettings.json，从解决方案根执行 `dotnet run --project src/KnowVault.DbMigrator`
				// 时工作目录会停在根目录，导致 appsettings.json 加载不到 —— OpenIddict 的应用客户端会被静默跳过
				// （表现为：scope 种子成功，但 OpenIddictApplications 0 行 → 登录报 invalid_client）。
				// 这里把内容根固定到输出目录，使行为不依赖启动时的工作目录。
				.UseContentRoot(AppContext.BaseDirectory)
				.AddAppSettingsSecretsJson() // 加载 User Secrets（用户机密）；开发环境下，敏感配置（如数据库密码）可以放在 secrets.json 中，不提交到 Git
				.ConfigureLogging((context, logging) => logging.ClearProviders())
				.ConfigureServices((hostContext, services) =>
				{
					services.AddHostedService<DbMigratorHostedService>();
				});
		}
	}

}
