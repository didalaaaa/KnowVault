using KnowVault.Domain;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.AuditLogging.EntityFrameworkCore;
using Volo.Abp.BackgroundJobs.EntityFrameworkCore;
using Volo.Abp.BlobStoring.Database.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.FeatureManagement.EntityFrameworkCore;
using Volo.Abp.Identity;
using Volo.Abp.Identity.EntityFrameworkCore;
using Volo.Abp.OpenIddict.EntityFrameworkCore;
using Volo.Abp.PermissionManagement.EntityFrameworkCore;
using Volo.Abp.SettingManagement.EntityFrameworkCore;
using Volo.Abp.TenantManagement;
using Volo.Abp.TenantManagement.EntityFrameworkCore;

namespace KnowVault.EntityFrameworkCore
{
	[ReplaceDbContext(typeof(IIdentityDbContext))]
	[ReplaceDbContext(typeof(ITenantManagementDbContext))]
	[ConnectionStringName("Default")]
	public class KnowVaultDbContext :
		AbpDbContext<KnowVaultDbContext>,
		ITenantManagementDbContext,
		IIdentityDbContext
	{
		/* Add DbSet properties for your Aggregate Roots / Entities here. */

		#region Entities from the modules

		/* Notice: We only implemented IIdentityProDbContext and ISaasDbContext
	     * and replaced them for this DbContext. This allows you to perform JOIN
	     * queries for the entities of these modules over the repositories easily. You
	     * typically don't need that for other modules. But, if you need, you can
	     * implement the DbContext interface of the needed module and use ReplaceDbContext
	     * attribute just like IIdentityProDbContext and ISaasDbContext.
	     *
	     * More info: Replacing a DbContext of a module ensures that the related module
	     * uses this DbContext on runtime. Otherwise, it will use its own DbContext class.
	     */

		// Identity
		public DbSet<IdentityUser> Users { get; set; }

		public DbSet<IdentityRole> Roles { get; set; }
		public DbSet<IdentityClaimType> ClaimTypes { get; set; }
		public DbSet<OrganizationUnit> OrganizationUnits { get; set; }
		public DbSet<IdentitySecurityLog> SecurityLogs { get; set; }
		public DbSet<IdentityLinkUser> LinkUsers { get; set; }
		public DbSet<IdentityUserDelegation> UserDelegations { get; set; }
		public DbSet<IdentitySession> Sessions { get; set; }

		// Tenant Management
		public DbSet<Tenant> Tenants { get; set; }

		public DbSet<TenantConnectionString> TenantConnectionStrings { get; set; }

		#endregion Entities from the modules

		#region KnowVault 业务实体

		public DbSet<KnowledgeBase> KnowledgeBases { get; set; }
		public DbSet<Document> Documents { get; set; }
		public DbSet<DocumentChunk> DocumentChunks { get; set; }
		public DbSet<Conversation> Conversations { get; set; }
		public DbSet<Message> Messages { get; set; }

		#endregion KnowVault 业务实体

		public KnowVaultDbContext(DbContextOptions<KnowVaultDbContext> options)
			: base(options)
		{
		}

		protected override void OnModelCreating(ModelBuilder builder)
		{
			base.OnModelCreating(builder);

			/* Include modules to your migration db context */

			builder.ConfigurePermissionManagement();
			builder.ConfigureSettingManagement();
			builder.ConfigureBackgroundJobs();
			builder.ConfigureAuditLogging();
			builder.ConfigureFeatureManagement();
			builder.ConfigureIdentity();
			builder.ConfigureOpenIddict();
			builder.ConfigureTenantManagement();
			builder.ConfigureBlobStoring();

			/* 业务实体的映射配置拆分到 Configurations 目录，按实体一个文件（IEntityTypeConfiguration） */
			builder.ApplyConfigurationsFromAssembly(typeof(KnowVaultDbContext).Assembly);
		}
	}
}
