using KnowVault.Domain;
using KnowVault.Domain.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace KnowVault.EntityFrameworkCore.Configurations
{
	/// <summary>
	/// 文档实体（<see cref="Document"/>）的数据库映射配置。
	/// </summary>
	public class DocumentConfiguration : IEntityTypeConfiguration<Document>
	{
		/// <summary>
		/// 配置表名、字段长度约束、枚举落库方式与查询索引。
		/// </summary>
		/// <param name="builder">实体类型构建器。</param>
		public void Configure(EntityTypeBuilder<Document> builder)
		{
			builder.ToTable(KnowVaultConsts.DbTablePrefix + "Documents", KnowVaultConsts.DbSchema);
			builder.ConfigureByConvention();

			builder.Property(x => x.Title).HasMaxLength(256).IsRequired();
			builder.Property(x => x.ObjectStorageKey).HasMaxLength(512);

			// 枚举以 int 落库，避免存字符串带来的排序与迁移成本
			builder.Property(x => x.SourceType).HasConversion<int>();
			builder.Property(x => x.Status).HasConversion<int>();

			// 文档列表按知识库 + 状态过滤
			builder.HasIndex(x => new { x.KnowledgeBaseId, x.Status });
		}
	}
}
