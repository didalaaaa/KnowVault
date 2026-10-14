using KnowVault.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
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

			// Qwen Embedding 走 HttpClient 直调 OpenAI 兼容端点，需要工厂来统一管理连接
			context.Services.AddHttpClient();

			// Kernel 用工厂构建：DeepSeek 走 OpenAI 兼容连接器（endpoint 指向 DeepSeek 官方地址）
			context.Services.AddSingleton(sp =>
			{
				var ai = sp.GetRequiredService<IOptions<AiOptions>>().Value;
				var builder = Kernel.CreateBuilder();
				builder.AddOpenAIChatCompletion(
					modelId: ai.DeepSeekModel,
					endpoint: new Uri(ai.DeepSeekBaseUrl),
					apiKey: ai.DeepSeekApiKey);
				return builder.Build();
			});
		}
	}

}
