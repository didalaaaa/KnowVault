using KnowVault.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace KnowVault.EntityFrameworkCore.Configurations
{
	/// <summary>
	/// 知识库实体（<see cref="KnowledgeBase"/>）的数据库映射配置。
	/// </summary>
	public class KnowledgeBaseConfiguration : IEntityTypeConfiguration<KnowledgeBase>
	{
		/// <summary>
		/// 配置表名、字段长度约束与多租户查询索引。
		/// </summary>
		/// <param name="builder">实体类型构建器。</param>
		public void Configure(EntityTypeBuilder<KnowledgeBase> builder)
		{
			builder.ToTable(KnowVaultConsts.DbTablePrefix + "KnowledgeBases", KnowVaultConsts.DbSchema);
			// 复用 ABP 约定：自动映射审计字段、主键、多租户与软删除等
			builder.ConfigureByConvention();

			builder.Property(x => x.Name).HasMaxLength(128).IsRequired();
			builder.Property(x => x.Description).HasMaxLength(512);
			builder.Property(x => x.SystemPrompt).HasMaxLength(4000);

			// 按租户隔离查询，索引与过滤条件保持一致
			builder.HasIndex(x => new { x.TenantId, x.Name });
		}
	}
}
