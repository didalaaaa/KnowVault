using KnowVault.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace KnowVault.EntityFrameworkCore.Configurations
{
	/// <summary>
	/// 知识库成员实体（<see cref="KnowledgeBaseMember"/>）的数据库映射配置
	/// </summary>
	public class KnowledgeBaseMemberConfiguration : IEntityTypeConfiguration<KnowledgeBaseMember>
	{
		/// <summary>
		/// 配置表名、唯一索引与常用查询索引
		/// </summary>
		/// <param name="builder">实体类型构建器</param>
		public void Configure(EntityTypeBuilder<KnowledgeBaseMember> builder)
		{
			builder.ToTable(KnowVaultConsts.DbTablePrefix + "KnowledgeBaseMembers", KnowVaultConsts.DbSchema);
			builder.ConfigureByConvention();

			// 同一用户在同一知识库只允许一条成员记录
			builder.HasIndex(x => new { x.KnowledgeBaseId, x.UserId }).IsUnique();
			// 按用户查「我能访问哪些库」是列表页的热路径
			builder.HasIndex(x => x.UserId);
		}
	}
}
