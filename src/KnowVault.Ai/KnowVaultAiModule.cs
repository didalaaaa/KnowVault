using KnowVault.Domain;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Modularity;

namespace KnowVault.Ai
{

	[DependsOn(
		typeof(KnowVaultDomainModule)
		)]
	public class KnowVaultAiModule : AbpModule
	{
		public override void ConfigureServices(ServiceConfigurationContext context)
		{
			var configuration = context.Services.GetConfiguration();

			// 模型路由配置：DeepSeek（生成）+ Qwen（Embedding/Rerank）
			Configure<AiOptions>(configuration.GetSection("Ai"));
		}
	}

}
