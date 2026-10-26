using KnowVault.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace KnowVault.EntityFrameworkCore.Configurations
{
	/// <summary>
	/// 消息实体（<see cref="Message"/>）的数据库映射配置。
	/// </summary>
	public class MessageConfiguration : IEntityTypeConfiguration<Message>
	{
		/// <summary>
		/// 配置表名、正文与引用溯源字段约束，以及按会话取消息的排序索引。
		/// </summary>
		/// <param name="builder">实体类型构建器。</param>
		public void Configure(EntityTypeBuilder<Message> builder)
		{
			builder.ToTable(KnowVaultConsts.DbTablePrefix + "Messages", KnowVaultConsts.DbSchema);
			builder.ConfigureByConvention();

			builder.Property(x => x.Content).IsRequired();
			// 引用片段带原文后体积不可控（topK 条 × 切片长度），CitationsJson 不设上限，否则长回答落库失败

			// 按会话按时间顺序回放消息
			builder.HasIndex(x => new { x.ConversationId, x.CreationTime });
		}
	}
}
