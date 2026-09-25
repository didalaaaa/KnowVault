using Volo.Abp.Modularity;
using KnowVault.Domain;
using KnowVault.EntityFrameworkCore;

namespace KnowVault.Ingest
{
	/// <summary>
	/// 文档摄入（Ingest）模块：负责文档解析 / 切片 / 向量化后写入 Qdrant。
	/// P0 仅建立项目与模块壳，RabbitMQ/MassTransit 消费队列在 P1 接入。
	/// </summary>
	[DependsOn(
		typeof(KnowVaultDomainModule),
		typeof(KnowVaultEntityFrameworkCoreModule)
		)]
	public class KnowVaultIngestModule : AbpModule
	{
	}
}
