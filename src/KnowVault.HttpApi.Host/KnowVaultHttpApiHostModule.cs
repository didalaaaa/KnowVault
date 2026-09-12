using KnowVault.Application;
using KnowVault.Application.Contracts;
using KnowVault.Domain;
using KnowVault.Domain.Shared;
using KnowVault.Domain.Shared.MultiTenancy;
using KnowVault.EntityFrameworkCore;
using KnowVault.HttpApi.Host.HealthChecks;
using KnowVault.Ingest;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Extensions.DependencyInjection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.OpenApi;
using OpenIddict.Server.AspNetCore;
using OpenIddict.Validation.AspNetCore;
using Volo.Abp;
using Volo.Abp.Account.Web;
using Volo.Abp.AspNetCore.MultiTenancy;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc.UI.Bundling;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.LeptonXLite;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.LeptonXLite.Bundling;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.Shared;
using Volo.Abp.AspNetCore.Serilog;
using Volo.Abp.Autofac;
using Volo.Abp.Modularity;
using Volo.Abp.OpenIddict;
using Volo.Abp.Security.Claims;
using Volo.Abp.Studio;
using Volo.Abp.Studio.Client.AspNetCore;
using Volo.Abp.Swashbuckle;
using Volo.Abp.UI.Navigation.Urls;
using Volo.Abp.VirtualFileSystem;

namespace KnowVault.HttpApi.Host
{
	[DependsOn(
		typeof(KnowVaultHttpApiModule),
		typeof(AbpStudioClientAspNetCoreModule),
		typeof(AbpAspNetCoreMvcUiLeptonXLiteThemeModule),
		typeof(AbpAutofacModule),
		typeof(AbpAspNetCoreMultiTenancyModule),
		typeof(KnowVaultApplicationModule),
		typeof(KnowVaultEntityFrameworkCoreModule),
		typeof(KnowVaultIngestModule),
		typeof(AbpAccountWebOpenIddictModule),
		typeof(AbpSwashbuckleModule),
		typeof(AbpAspNetCoreSerilogModule)
		)]
	public class KnowVaultHttpApiHostModule : AbpModule
	{
		public override void PreConfigureServices(ServiceConfigurationContext context)
		{
			var hostingEnvironment = context.Services.GetHostingEnvironment();
			var configuration = context.Services.GetConfiguration();

			// 令牌验证 相关的设置
			PreConfigure<OpenIddictBuilder>(builder =>
			{
				builder.AddValidation(options =>
				{
					options.AddAudiences("KnowVault");
					options.UseLocalServer();
					options.UseAspNetCore();
				});
			});

			if (!hostingEnvironment.IsDevelopment())
			{
				PreConfigure<AbpOpenIddictAspNetCoreOptions>(options =>
				{
					// 生产环境强制关闭开发证书选项，避免非开发环境使用不安全的临时证书
					options.AddDevelopmentEncryptionAndSigningCertificate = false;
				});

				PreConfigure<OpenIddictServerBuilder>(serverBuilder =>
				{
					serverBuilder.AddProductionEncryptionAndSigningCertificate("openiddict.pfx", configuration["AuthServer:CertificatePassPhrase"]!);
					serverBuilder.SetIssuer(new Uri(configuration["AuthServer:Authority"]!));
				});
			}
		}

		public override void ConfigureServices(ServiceConfigurationContext context)
		{
			var configuration = context.Services.GetConfiguration();
			var hostingEnvironment = context.Services.GetHostingEnvironment();

			// 配置个人信息显示，生产环境不建议启用
			if (!configuration.GetValue<bool>("App:DisablePII"))
			{
				// JWT令牌验证失败时，能够输出详细错误，如：哪个Claim验证失败、具体原因是什么
				Microsoft.IdentityModel.Logging.IdentityModelEventSource.ShowPII = true;
				// 是否在日志中输出完整的安全令牌
				Microsoft.IdentityModel.Logging.IdentityModelEventSource.LogCompleteSecurityArtifact = true;
			}

			// 通常用于开发环境（不使用 HTTPS）或使用反向代理（如 Nginx）的场景
			if (!configuration.GetValue<bool>("AuthServer:RequireHttpsMetadata"))
			{
				Configure<OpenIddictServerAspNetCoreOptions>(options =>
				{
					// 禁用 OpenIddict 的 HTTPS 要求
					options.DisableTransportSecurityRequirement = true;
				});

				Configure<ForwardedHeadersOptions>(options =>
				{
					// 告诉 ASP.NET Core，从请求头 X-Forwarded-Proto 中读取客户端实际使用的协议（HTTP 或 HTTPS）
					options.ForwardedHeaders = ForwardedHeaders.XForwardedProto;
					// 清空已知代理列表，在开发环境中避免复杂的代理判断逻辑
					options.KnownIPNetworks.Clear();
					options.KnownProxies.Clear();
				});
			}

			if (hostingEnvironment.IsDevelopment())
			{
				// AddRazorRuntimeCompilation 允许在开发环境下修改 .cshtml 文件后，无需重启应用即可看到页面变化
				context.Services.AddRazorPages()
					.AddRazorRuntimeCompilation();
			}

			ConfigureStudio(hostingEnvironment);
			ConfigureAuthentication(context);
			ConfigureUrls(configuration);
			ConfigureBundles(hostingEnvironment);
			ConfigureConventionalControllers();
			ConfigureHealthChecks(context);
			ConfigureSwagger(context, configuration);
			ConfigureVirtualFileSystem(context);
			ConfigureCors(context, configuration);
		}

		private void ConfigureStudio(IHostEnvironment hostingEnvironment)
		{
			if (hostingEnvironment.IsProduction())
			{
				Configure<AbpStudioClientOptions>(options =>
				{
					// 是否启用 ABP Studio 与当前应用之间的链路连接，默认启用
					options.IsLinkEnabled = false;
				});
			}
		}

		private void ConfigureAuthentication(ServiceConfigurationContext context)
		{
			context.Services.ForwardIdentityAuthenticationForBearer(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
			context.Services.Configure<AbpClaimsPrincipalFactoryOptions>(options =>
			{
				options.IsDynamicClaimsEnabled = true;
			});
		}

		private void ConfigureUrls(IConfiguration configuration)
		{
			Configure<AppUrlOptions>(options =>
			{
				options.Applications["MVC"].RootUrl = configuration["App:SelfUrl"];
				options.RedirectAllowedUrls.AddRange(configuration["App:RedirectAllowedUrls"]?.Split(',') ?? Array.Empty<string>());
			});
		}

		private void ConfigureBundles(IHostEnvironment hostingEnvironment)
		{
			Configure<AbpBundlingOptions>(options =>
			{
				options.StyleBundles.Configure(
					LeptonXLiteThemeBundles.Styles.Global,
					bundle =>
					{
						bundle.AddFiles("/global-styles.css");
					}
				);

				options.ScriptBundles.Configure(
					LeptonXLiteThemeBundles.Scripts.Global,
					bundle =>
					{
						bundle.AddFiles("/global-scripts.js");
						if (hostingEnvironment.IsDevelopment())
						{
							bundle.AddFiles("/dev-login-helper.js");
						}
					}
				);
			});
		}

		private void ConfigureVirtualFileSystem(ServiceConfigurationContext context)
		{
			var hostingEnvironment = context.Services.GetHostingEnvironment();

			if (hostingEnvironment.IsDevelopment())
			{
				Configure<AbpVirtualFileSystemOptions>(options =>
				{
					options.FileSets.ReplaceEmbeddedByPhysical<KnowVaultDomainSharedModule>(Path.Combine(hostingEnvironment.ContentRootPath, $"..{Path.DirectorySeparatorChar}KnowVault.Domain.Shared"));
					options.FileSets.ReplaceEmbeddedByPhysical<KnowVaultDomainModule>(Path.Combine(hostingEnvironment.ContentRootPath, $"..{Path.DirectorySeparatorChar}KnowVault.Domain"));
					options.FileSets.ReplaceEmbeddedByPhysical<KnowVaultApplicationContractsModule>(Path.Combine(hostingEnvironment.ContentRootPath, $"..{Path.DirectorySeparatorChar}KnowVault.Application.Contracts"));
					options.FileSets.ReplaceEmbeddedByPhysical<KnowVaultApplicationModule>(Path.Combine(hostingEnvironment.ContentRootPath, $"..{Path.DirectorySeparatorChar}KnowVault.Application"));
				});
			}
		}

		private void ConfigureConventionalControllers()
		{
			Configure<AbpAspNetCoreMvcOptions>(options =>
			{
				options.ConventionalControllers.Create(typeof(KnowVaultApplicationModule).Assembly);
			});
		}

		private static void ConfigureSwagger(ServiceConfigurationContext context, IConfiguration configuration)
		{
			context.Services.AddAbpSwaggerGenWithOidc(
				configuration["AuthServer:Authority"]!,
				["KnowVault"],
				[AbpSwaggerOidcFlows.AuthorizationCode],
				null,
				options =>
				{
					options.SwaggerDoc("v1", new OpenApiInfo { Title = "KnowVault API", Version = "v1" });
					options.DocInclusionPredicate((docName, description) => true);
					options.CustomSchemaIds(type => type.FullName);
				});
		}

		private void ConfigureCors(ServiceConfigurationContext context, IConfiguration configuration)
		{
			context.Services.AddCors(options =>
			{
				options.AddDefaultPolicy(builder =>
				{
					builder
						.WithOrigins(
							configuration["App:CorsOrigins"]?
								.Split(",", StringSplitOptions.RemoveEmptyEntries)
								.Select(o => o.Trim().RemovePostFix("/"))
								.ToArray() ?? Array.Empty<string>()
						)
						.WithAbpExposedHeaders()
						.SetIsOriginAllowedToAllowWildcardSubdomains()
						.AllowAnyHeader()
						.AllowAnyMethod()
						.AllowCredentials();
				});
			});
		}

		private void ConfigureHealthChecks(ServiceConfigurationContext context)
		{
			context.Services.AddKnowVaultHealthChecks();
		}

		public override void OnApplicationInitialization(ApplicationInitializationContext context)
		{
			var app = context.GetApplicationBuilder();
			var env = context.GetEnvironment();

			app.UseForwardedHeaders();

			if (env.IsDevelopment())
			{
				app.UseDeveloperExceptionPage();
			}

			app.UseAbpRequestLocalization();

			if (!env.IsDevelopment())
			{
				app.UseErrorPage();
			}

			app.UseRouting();
			app.MapAbpStaticAssets();
			app.UseAbpStudioLink();
			app.UseAbpSecurityHeaders();
			app.UseCors();
			app.UseAuthentication();
			app.UseAbpOpenIddictValidation();

			if (MultiTenancyConsts.IsEnabled)
			{
				app.UseMultiTenancy();
			}

			app.UseUnitOfWork();
			app.UseDynamicClaims();
			app.UseAuthorization();

			app.UseSwagger();
			app.UseAbpSwaggerUI(options =>
			{
				options.SwaggerEndpoint("/swagger/v1/swagger.json", "KnowVault API");

				var configuration = context.ServiceProvider.GetRequiredService<IConfiguration>();
				options.OAuthClientId(configuration["AuthServer:SwaggerClientId"]);
			});
			app.UseAuditing();
			app.UseAbpSerilogEnrichers();
			app.UseConfiguredEndpoints();
		}
	}
}
