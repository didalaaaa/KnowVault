using KnowVault.Domain;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Volo.Abp;
using Volo.Abp.Data;

namespace KnowVault.DbMigrator
{

	public class DbMigratorHostedService : IHostedService
	{
		private readonly IHostApplicationLifetime _hostApplicationLifetime;
		private readonly IConfiguration _configuration;

		public DbMigratorHostedService(IHostApplicationLifetime hostApplicationLifetime, IConfiguration configuration)
		{
			_hostApplicationLifetime = hostApplicationLifetime;
			_configuration = configuration;
		}

		public async Task StartAsync(CancellationToken cancellationToken)
		{
			using (var application = await AbpApplicationFactory.CreateAsync<KnowVaultDbMigratorModule>(options =>
			{
				// 用 Host 的配置替换 ABP 应用内部的配置，避免重复加载
				options.Services.ReplaceConfiguration(_configuration);
				options.UseAutofac();
				options.Services.AddLogging(c => c.AddSerilog());

				// 标记为数据迁移环境，避免不需要的环境加载（如：跳过 IBackgroundWorker、IHostedService）
				options.AddDataMigrationEnvironment();
			}))
			{
				// 加载所有模块（按 [DependsOn] 顺序）
				// 构建 DI 容器，注册所有服务
				await application.InitializeAsync();

				await application
					.ServiceProvider
					.GetRequiredService<KnowVaultDbMigrationService>()
					.MigrateAsync();

				// 关闭 ABP 应用
				await application.ShutdownAsync();

				// 停止 Host
				_hostApplicationLifetime.StopApplication();
			}
		}

		public Task StopAsync(CancellationToken cancellationToken)
		{
			return Task.CompletedTask;
		}
	}

}
