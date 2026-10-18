using KnowVault.Domain;
using Volo.Abp;
using Volo.Abp.Autofac;
using Volo.Abp.Data;
using Volo.Abp.Testing;
using Xunit;

namespace KnowVault.IntegrationTests
{
	/// <summary>
	/// 集成测试基类：启动 ABP 应用、迁移数据库、执行种子
	/// </summary>
	public abstract class KnowVaultIntegrationTestBase : AbpIntegratedTest<KnowVaultIntegrationTestModule>, IAsyncLifetime
	{
		/// <inheritdoc />
		protected override void SetAbpApplicationCreationOptions(AbpApplicationCreationOptions options)
			=> options.UseAutofac();

		/// <inheritdoc />
		public async Task InitializeAsync()
		{
			await GetRequiredService<IKnowVaultDbSchemaMigrator>().MigrateAsync();
			await GetRequiredService<IDataSeeder>().SeedAsync();
		}

		/// <inheritdoc />
		public Task DisposeAsync() => Task.CompletedTask;
	}
}
