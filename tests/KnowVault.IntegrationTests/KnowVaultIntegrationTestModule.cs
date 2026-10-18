using System.Collections.Generic;
using KnowVault.Ai;
using KnowVault.Application;
using KnowVault.Domain;
using KnowVault.EntityFrameworkCore;
using KnowVault.Ingest;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Volo.Abp;
using Volo.Abp.Autofac;
using Volo.Abp.Data;
using Volo.Abp.Modularity;
using Volo.Abp.Testing;

namespace KnowVault.IntegrationTests
{
	/// <summary>
	/// 集成测试模块：真实 Postgres/Qdrant/MinIO + 假 Embedding + 假消息发布器
	/// </summary>
	[DependsOn(
		typeof(AbpTestBaseModule),
		typeof(AbpAutofacModule),
		typeof(KnowVaultApplicationModule),
		typeof(KnowVaultEntityFrameworkCoreModule),
		typeof(KnowVaultIngestModule))]
	public class KnowVaultIntegrationTestModule : AbpModule
	{
		/// <inheritdoc />
		public override void PreConfigureServices(ServiceConfigurationContext context)
		{
			Configure<AbpDbConnectionOptions>(options =>
				options.ConnectionStrings.Default = TestEnvironment.Current.PostgresConnectionString);
		}

		/// <inheritdoc />
		public override void ConfigureServices(ServiceConfigurationContext context)
		{
			var environment = TestEnvironment.Current;
			var configuration = new ConfigurationBuilder()
				.AddConfiguration(context.Services.GetConfiguration())
				.AddInMemoryCollection(new Dictionary<string, string?>
				{
					["ConnectionStrings:Default"] = environment.PostgresConnectionString,
					["ConnectionStrings:rabbitmq"] = environment.RabbitMqConnectionString,
					["Qdrant:Endpoint"] = environment.QdrantEndpoint,
					["Qdrant:VectorSize"] = "1024",
					["Qdrant:CollectionName"] = "knowvault_chunks_test",
					["Minio:Endpoint"] = environment.MinioEndpoint,
					["Minio:AccessKey"] = environment.MinioAccessKey,
					["Minio:SecretKey"] = environment.MinioSecretKey,
					["Minio:BucketName"] = "knowvault-test",
					["Minio:UseSsl"] = "false"
				})
				.Build();

			context.Services.ReplaceConfiguration(configuration);

			// EF 模块在依赖初始化时已用旧配置绑定过 Qdrant/Minio，这里在依赖之后覆盖，确保容器端点生效
			Configure<QdrantOptions>(options =>
			{
				options.Endpoint = environment.QdrantEndpoint;
				options.VectorSize = 1024;
				options.CollectionName = "knowvault_chunks_test";
			});
			Configure<MinioOptions>(options =>
			{
				options.Endpoint = environment.MinioEndpoint;
				options.AccessKey = environment.MinioAccessKey;
				options.SecretKey = environment.MinioSecretKey;
				options.BucketName = "knowvault-test";
				options.UseSsl = false;
			});

			context.Services.Replace(ServiceDescriptor.Singleton<IEmbeddingService, FakeEmbeddingService>());
			context.Services.Replace(ServiceDescriptor.Singleton<IDocumentIngestPublisher, FakeDocumentIngestPublisher>());
			// 集成测试不跑登录流程；授权拦截器放行，ACL 仍由 KnowledgeBaseAccessChecker 真实校验
			context.Services.AddAlwaysAllowAuthorization();
			if (!TestEnvironment.MinioAvailable)
			{
				context.Services.Replace(ServiceDescriptor.Singleton<IDocumentStorage, FakeDocumentStorage>());
			}
		}
	}
}
