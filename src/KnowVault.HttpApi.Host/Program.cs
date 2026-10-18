using Serilog;

namespace KnowVault.HttpApi.Host
{
	public class Program
	{
		public static async Task<int> Main(string[] args)
		{
			Log.Logger = new LoggerConfiguration()
				.WriteTo.Async(c => c.File("Logs/logs.txt"))
				.WriteTo.Async(c => c.Console())
				.CreateBootstrapLogger();

			try
			{
				Log.Information("Starting KnowVault.HttpApi.Host.");
				var builder = WebApplication.CreateBuilder(args);
				builder.Host
					.AddAppSettingsSecretsJson()
					.UseAutofac()
					.UseSerilog((context, services, loggerConfiguration) =>
					{
						loggerConfiguration
							.ReadFrom.Configuration(context.Configuration)
							.ReadFrom.Services(services)
							.Enrich.With<ActivityTraceEnricher>()
							.WriteTo.Async(c => c.AbpStudio(services));

						// Seq 地址由 Aspire 注入；本地单独起 API 时可读 appsettings 的 Seq:ServerUrl
						var seqUrl = context.Configuration["Seq:ServerUrl"];
						if (!string.IsNullOrWhiteSpace(seqUrl))
						{
							loggerConfiguration.WriteTo.Seq(seqUrl);
						}
					});
				await builder.AddApplicationAsync<KnowVaultHttpApiHostModule>();
				var app = builder.Build();
				await app.InitializeApplicationAsync();
				await app.RunAsync();
				return 0;
			}
			catch (Exception ex)
			{
				if (ex is HostAbortedException)
				{
					throw;
				}

				Log.Fatal(ex, "Host terminated unexpectedly!");
				return 1;
			}
			finally
			{
				Log.CloseAndFlush();
			}
		}
	}
}
