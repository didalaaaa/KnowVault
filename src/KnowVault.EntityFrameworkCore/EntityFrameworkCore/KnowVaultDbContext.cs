using Microsoft.EntityFrameworkCore;
using Volo.Abp.AuditLogging.EntityFrameworkCore;
using Volo.Abp.BackgroundJobs.EntityFrameworkCore;
using Volo.Abp.BlobStoring.Database.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;
using Volo.Abp.FeatureManagement.EntityFrameworkCore;
using Volo.Abp.Identity;
using Volo.Abp.Identity.EntityFrameworkCore;
using Volo.Abp.PermissionManagement.EntityFrameworkCore;
using Volo.Abp.SettingManagement.EntityFrameworkCore;
using Volo.Abp.OpenIddict.EntityFrameworkCore;
using Volo.Abp.TenantManagement;
using Volo.Abp.TenantManagement.EntityFrameworkCore;
using KnowVault.Domain;
using KnowVault.Domain.Chat;
using KnowVault.Domain.Documents;
using KnowVault.Domain.KnowledgeBases;
using KnowVault.Domain.Shared.Chat;
using KnowVault.Domain.Shared.Documents;

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

	    #endregion

	    #region KnowVault 业务实体

	    public DbSet<KnowledgeBase> KnowledgeBases { get; set; }
	    public DbSet<Document> Documents { get; set; }
	    public DbSet<DocumentChunk> DocumentChunks { get; set; }
	    public DbSet<Conversation> Conversations { get; set; }
	    public DbSet<Message> Messages { get; set; }

	    #endregion

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

	        /* Configure your own tables/entities inside here */

	        builder.Entity<KnowledgeBase>(b =>
	        {
	            b.ToTable(KnowVaultConsts.DbTablePrefix + "KnowledgeBases", KnowVaultConsts.DbSchema);
	            b.ConfigureByConvention();
	            b.Property(x => x.Name).HasMaxLength(128).IsRequired();
	            b.Property(x => x.Description).HasMaxLength(512);
	            b.HasIndex(x => new { x.TenantId, x.Name });
	        });

	        builder.Entity<Document>(b =>
	        {
	            b.ToTable(KnowVaultConsts.DbTablePrefix + "Documents", KnowVaultConsts.DbSchema);
	            b.ConfigureByConvention();
	            b.Property(x => x.Title).HasMaxLength(256).IsRequired();
	            b.Property(x => x.ObjectStorageKey).HasMaxLength(512);
	            b.Property(x => x.SourceType).HasConversion<int>();
	            b.Property(x => x.Status).HasConversion<int>();
	            b.HasIndex(x => new { x.KnowledgeBaseId, x.Status });
	        });

	        builder.Entity<DocumentChunk>(b =>
	        {
	            b.ToTable(KnowVaultConsts.DbTablePrefix + "DocumentChunks", KnowVaultConsts.DbSchema);
	            b.ConfigureByConvention();
	            b.Property(x => x.Content).IsRequired();
	            b.HasIndex(x => new { x.DocumentId, x.ChunkIndex });
	        });

	        builder.Entity<Conversation>(b =>
	        {
	            b.ToTable(KnowVaultConsts.DbTablePrefix + "Conversations", KnowVaultConsts.DbSchema);
	            b.ConfigureByConvention();
	            b.Property(x => x.Title).HasMaxLength(256).IsRequired();
	            b.HasIndex(x => x.TenantId);
	        });

	        builder.Entity<Message>(b =>
	        {
	            b.ToTable(KnowVaultConsts.DbTablePrefix + "Messages", KnowVaultConsts.DbSchema);
	            b.ConfigureByConvention();
	            b.Property(x => x.Content).IsRequired();
	            b.Property(x => x.CitationsJson).HasMaxLength(4000);
	            b.HasIndex(x => new { x.ConversationId, x.CreationTime });
	        });
	    }
	}

}
