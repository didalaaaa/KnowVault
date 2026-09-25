using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace KnowVault.EntityFrameworkCore
{
	/// <summary>
	/// This class is needed for EF Core console commands
	/// (like Add-Migration and Update - Database commands)
	/// </summary>
	public class KnowVaultDbContextFactory : IDesignTimeDbContextFactory<KnowVaultDbContext>
	{
		public KnowVaultDbContext CreateDbContext(string[] args)
		{
			// Npgsql 6.0+ 默认使用UTC时间，旧版使用的是不带时区的时间，加上这个使用旧版时间
			// https://www.npgsql.org/efcore/release-notes/6.0.html#opting-out-of-the-new-timestamp-mapping-logic
			AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

			var configuration = BuildConfiguration();

			// 配置实体扩展映射；ABP 的模块化实体扩展系统允许其他模块给已有实体添加字段（如给 IdentityUser 加 AvatarUrl）
			KnowVaultEfCoreEntityExtensionMappings.Configure();

			var builder = new DbContextOptionsBuilder<KnowVaultDbContext>()
				.UseNpgsql(configuration.GetConnectionString("Default"));

			return new KnowVaultDbContext(builder.Options);
		}

		private static IConfigurationRoot BuildConfiguration()
		{
			var builder = new ConfigurationBuilder()
				// Directory.GetCurrentDirectory() 执行命令时所在的目录
				.SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "../KnowVault.DbMigrator/"))
				.AddJsonFile("appsettings.json", optional: false)
				.AddEnvironmentVariables();

			return builder.Build();
		}
	}
}
