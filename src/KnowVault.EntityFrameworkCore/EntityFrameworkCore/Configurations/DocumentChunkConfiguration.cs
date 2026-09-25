using KnowVault.Domain;
using KnowVault.Domain.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace KnowVault.EntityFrameworkCore.Configurations
{
	/// <summary>
	/// 文档切片实体（<see cref="DocumentChunk"/>）的数据库映射配置。
	/// </summary>
	public class DocumentChunkConfiguration : IEntityTypeConfiguration<DocumentChunk>
	{
		/// <summary>
		/// 配置表名、正文必填约束与切片定位索引。
		/// </summary>
		/// <param name="builder">实体类型构建器。</param>
		public void Configure(EntityTypeBuilder<DocumentChunk> builder)
		{
			builder.ToTable(KnowVaultConsts.DbTablePrefix + "DocumentChunks", KnowVaultConsts.DbSchema);
			builder.ConfigureByConvention();

			builder.Property(x => x.Content).IsRequired();

			// 按文档取切片并按序号排序，支撑引用溯源
			builder.HasIndex(x => new { x.DocumentId, x.ChunkIndex });
		}
	}
}
