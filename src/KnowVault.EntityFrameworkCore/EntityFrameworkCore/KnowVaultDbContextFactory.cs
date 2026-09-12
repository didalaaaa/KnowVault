using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace KnowVault.EntityFrameworkCore
{

	/* This class is needed for EF Core console commands
	 * (like Add-Migration and Update-Database commands) */
	public class KnowVaultDbContextFactory : IDesignTimeDbContextFactory<KnowVaultDbContext>
	{
	    public KnowVaultDbContext CreateDbContext(string[] args)
	    {
	        // https://www.npgsql.org/efcore/release-notes/6.0.html#opting-out-of-the-new-timestamp-mapping-logic
	        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

	        var configuration = BuildConfiguration();

	        KnowVaultEfCoreEntityExtensionMappings.Configure();

	        var builder = new DbContextOptionsBuilder<KnowVaultDbContext>()
	            .UseNpgsql(configuration.GetConnectionString("Default"));

	        return new KnowVaultDbContext(builder.Options);
	    }

	    private static IConfigurationRoot BuildConfiguration()
	    {
	        var builder = new ConfigurationBuilder()
	            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "../KnowVault.DbMigrator/"))
	            .AddJsonFile("appsettings.json", optional: false)
	            .AddEnvironmentVariables();

	        return builder.Build();
	    }
	}

}
