using KnowVault.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace KnowVault.EntityFrameworkCore.Configurations
{
	/// <summary>
	/// AI 用量记录（<see cref="AiUsageRecord"/>）的数据库映射配置
	/// </summary>
	public class AiUsageRecordConfiguration : IEntityTypeConfiguration<AiUsageRecord>
	{
		/// <summary>
		/// 配置表名、字段长度、费用精度与查询索引
		/// </summary>
		/// <param name="builder">实体类型构建器</param>
		public void Configure(EntityTypeBuilder<AiUsageRecord> builder)
		{
			builder.ToTable(KnowVaultConsts.DbTablePrefix + "AiUsageRecords", KnowVaultConsts.DbSchema);
			builder.ConfigureByConvention();

			builder.Property(x => x.Model).IsRequired().HasMaxLength(128);
			builder.Property(x => x.EstimatedCostCny).HasColumnType("decimal(18,6)");

			// 仪表盘按时间取最近 7 天
			builder.HasIndex(x => x.CreationTime);
			// 按库聚合与文档删除后的历史查询
			builder.HasIndex(x => new { x.KnowledgeBaseId, x.CreationTime });
			// 同一次问答的多条记录按 AskId 串起来
			builder.HasIndex(x => x.AskId);
		}
	}
}
