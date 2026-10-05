using KnowVault.Domain;
using KnowVault.EntityFrameworkCore;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.Modularity;

namespace KnowVault.Ingest
{
	/// <summary>
	/// 文档摄入（Ingest）模块：负责文档解析 / 切片 / 向量化后写入 Qdrant。
	/// P1 采用进程内 MassTransit consumer（消息仍走 RabbitMQ），processor 与 consumer 保持宿主无关，
	/// P3 可将本模块平移到独立 Worker 宿主。
	/// </summary>
	[DependsOn(
		typeof(KnowVaultDomainModule),
		typeof(KnowVaultEntityFrameworkCoreModule)
		)]
	public class KnowVaultIngestModule : AbpModule
	{
		/// <inheritdoc />
		public override void ConfigureServices(ServiceConfigurationContext context)
		{
			var configuration = context.Services.GetConfiguration();
			Configure<IngestOptions>(configuration.GetSection("Ingest"));

			context.Services.AddMassTransit(bus =>
			{
				bus.AddConsumer<DocumentIngestConsumer>();

				bus.UsingRabbitMq((busContext, rabbit) =>
				{
					// Aspire 的 WithReference 会注入 amqp://guest:guest@host:port
					var connectionString = configuration.GetConnectionString("rabbitmq");
					if (connectionString.IsNullOrWhiteSpace())
					{
						throw new AbpInitializationException(
							"未配置 RabbitMQ：需要 ConnectionStrings:rabbitmq（由 Aspire 的 WithReference 注入）。");
					}

					rabbit.Host(new Uri(connectionString));
					rabbit.ConfigureEndpoints(busContext);
				});
			});

			// 只在 Ingest 模块做 Qdrant 初始化：DbMigrator 不加载本模块，避免迁移时去连向量库
			context.Services.AddHostedService<QdrantCollectionInitializer>();
		}
	}
}
