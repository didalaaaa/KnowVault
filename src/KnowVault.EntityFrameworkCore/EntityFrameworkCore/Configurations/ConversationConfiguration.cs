using KnowVault.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace KnowVault.EntityFrameworkCore.Configurations
{
	/// <summary>
	/// 会话实体（<see cref="Conversation"/>）的数据库映射配置。
	/// </summary>
	public class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
	{
		/// <summary>
		/// 配置表名、标题长度约束与多租户查询索引。
		/// </summary>
		/// <param name="builder">实体类型构建器。</param>
		public void Configure(EntityTypeBuilder<Conversation> builder)
		{
			builder.ToTable(KnowVaultConsts.DbTablePrefix + "Conversations", KnowVaultConsts.DbSchema);
			builder.ConfigureByConvention();

			builder.Property(x => x.Title).HasMaxLength(256).IsRequired();
			builder.Property(x => x.KnowledgeBaseScopeJson).HasColumnType("text");
			builder.Property(x => x.ScopeAll).HasDefaultValue(false);

			// 会话列表按租户过滤
			builder.HasIndex(x => x.TenantId);

			// 个人会话列表按「用户 + 最近活动」查询
			builder.HasIndex(x => new { x.CreatorId, x.LastMessageAt });
		}
	}
}
