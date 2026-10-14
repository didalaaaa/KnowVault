using KnowVault.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Minio;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using Volo.Abp;
using Volo.Abp.AuditLogging.EntityFrameworkCore;
using Volo.Abp.BackgroundJobs.EntityFrameworkCore;
using Volo.Abp.BlobStoring.Database.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.PostgreSql;
using Volo.Abp.FeatureManagement.EntityFrameworkCore;
using Volo.Abp.Identity.EntityFrameworkCore;
using Volo.Abp.Modularity;
using Volo.Abp.OpenIddict.EntityFrameworkCore;
using Volo.Abp.PermissionManagement.EntityFrameworkCore;
using Volo.Abp.SettingManagement.EntityFrameworkCore;
using Volo.Abp.Studio;
using Volo.Abp.TenantManagement.EntityFrameworkCore;

namespace KnowVault.EntityFrameworkCore
{
	[DependsOn(
		typeof(KnowVaultDomainModule),
		typeof(AbpPermissionManagementEntityFrameworkCoreModule),
		typeof(AbpSettingManagementEntityFrameworkCoreModule),
		typeof(AbpEntityFrameworkCorePostgreSqlModule),
		typeof(AbpBackgroundJobsEntityFrameworkCoreModule),
		typeof(AbpAuditLoggingEntityFrameworkCoreModule),
		typeof(AbpFeatureManagementEntityFrameworkCoreModule),
		typeof(AbpIdentityEntityFrameworkCoreModule),
		typeof(AbpOpenIddictEntityFrameworkCoreModule),
		typeof(AbpTenantManagementEntityFrameworkCoreModule),
		typeof(BlobStoringDatabaseEntityFrameworkCoreModule)
		)]
	public class KnowVaultEntityFrameworkCoreModule : AbpModule
	{
		public override void PreConfigureServices(ServiceConfigurationContext context)
		{
			// https://www.npgsql.org/efcore/release-notes/6.0.html#opting-out-of-the-new-timestamp-mapping-logic
			AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

			KnowVaultEfCoreEntityExtensionMappings.Configure();
		}

		public override void ConfigureServices(ServiceConfigurationContext context)
		{
			context.Services.AddAbpDbContext<KnowVaultDbContext>(options =>
			{
				/* Remove "includeAllEntities: true" to create
				 * default repositories only for aggregate roots 
				 * includeAllEntities：
				 * true：为所有实体（包括非聚合根）生成默认仓储
				 * false：只为聚合根（继承 AggregateRoot 的类）生成默认仓储
				 * 这是 ABP 模板的提醒：生产项目建议改为 false，遵循 DDD 原则，只为聚合根提供仓储。true 只是方便快速开发。
				 */
				options.AddDefaultRepositories(includeAllEntities: false);
			});

			// Qdrant（向量库）与 MinIO（对象存储）的连接参数；密钥由 AppHost 注入
			var configuration = context.Services.GetConfiguration();
			Configure<QdrantOptions>(configuration.GetSection("Qdrant"));
			Configure<MinioOptions>(configuration.GetSection("Minio"));

			// Qdrant 官方 gRPC 客户端（单例，内部持有 channel）
			context.Services.AddSingleton(sp =>
			{
				var qdrant = sp.GetRequiredService<IOptions<QdrantOptions>>().Value;
				var clientConfiguration = new ClientConfiguration();
				if (!string.IsNullOrWhiteSpace(qdrant.ApiKey))
				{
					clientConfiguration.ApiKey = qdrant.ApiKey!;
				}

				var channel = QdrantChannel.ForAddress(qdrant.Endpoint, clientConfiguration);
				return new QdrantClient(new QdrantGrpcClient(channel));
			});

			// MinIO 客户端（单例）；连接信息与密钥由 AppHost 注入，缺失时给出明确错误
			context.Services.AddSingleton<IMinioClient>(sp =>
			{
				var minio = sp.GetRequiredService<IOptions<MinioOptions>>().Value;
				if (string.IsNullOrWhiteSpace(minio.Endpoint) || string.IsNullOrWhiteSpace(minio.AccessKey))
				{
					throw new AbpInitializationException("MinIO 未配置：需要 Minio:Endpoint / Minio:AccessKey / Minio:SecretKey（由 AppHost 注入）。");
				}

				return new MinioClient()
					.WithEndpoint(minio.Endpoint)
					.WithCredentials(minio.AccessKey, minio.SecretKey)
					.WithSSL(minio.UseSsl)
					.Build();
			});

			// ABP Studio 是一个可视化开发工具，它在"分析模式"下会扫描项目结构，但不需要真正连接数据库
			if (AbpStudioAnalyzeHelper.IsInAnalyzeMode)
			{
				return;
			}

			Configure<AbpDbContextOptions>(options =>
			{
				/* The main point to change your DBMS.
	             * See also KnowVaultDbContextFactory for EF Core tooling. */

				options.UseNpgsql();

			});

		}
	}
}
