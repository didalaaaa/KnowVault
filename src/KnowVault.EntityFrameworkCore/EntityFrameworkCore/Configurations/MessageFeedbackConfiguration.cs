using KnowVault.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace KnowVault.EntityFrameworkCore.Configurations
{
	/// <summary>
	/// 消息反馈实体（<see cref="MessageFeedback"/>）的数据库映射配置
	/// </summary>
	public class MessageFeedbackConfiguration : IEntityTypeConfiguration<MessageFeedback>
	{
		/// <summary>
		/// 配置表名、备注长度与「同一用户同一消息只保留一条」的唯一索引
		/// </summary>
		/// <param name="builder">实体类型构建器</param>
		public void Configure(EntityTypeBuilder<MessageFeedback> builder)
		{
			builder.ToTable(KnowVaultConsts.DbTablePrefix + "MessageFeedbacks", KnowVaultConsts.DbSchema);
			builder.ConfigureByConvention();

			builder.Property(x => x.Comment).HasMaxLength(1000);

			// 同一用户对同一消息只允许一条反馈，切换赞/踩走更新而不是新增
			builder.HasIndex(x => new { x.MessageId, x.UserId }).IsUnique();
			// 会话删除与消息历史回填都按消息 ID 批量取
			builder.HasIndex(x => x.MessageId);
		}
	}
}
