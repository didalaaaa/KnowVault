using KnowVault.Application;
using KnowVault.Application.Contracts;
using KnowVault.Domain;
using KnowVault.Domain.Shared;
using KnowVault.EntityFrameworkCore;
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
using Volo.Abp.AspNetCore.Mvc.Libs;
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
					// 加载 openiddict.pfx 证书文件，密码从 AuthServer:CertificatePassPhrase 读取
					serverBuilder.AddProductionEncryptionAndSigningCertificate("openiddict.pfx", configuration["AuthServer:CertificatePassPhrase"]!);
					// 设置令牌的签发者 URL
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
			ConfigureMvcLibs();

			// SignalR 服务端在 .NET 共享框架里，不需要额外 NuGet 包；Hub 在 OnApplicationInitialization 末端映射
			context.Services.AddSignalR();
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
			// 配置ABP扩展的 Bearer Token 认证；OpenIddictValidationAspNetCoreDefaults 默认验证行为
			context.Services.ForwardIdentityAuthenticationForBearer(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
			context.Services.Configure<AbpClaimsPrincipalFactoryOptions>(options =>
			{
				/*
				 * 启用动态 Claims，AbpClaimsPrincipalFactoryOptions 控制如何构建 ClaimsPrincipal（即当前用户的身份对象）；
				 * 动态 Claims 解决：
				 * 1、用户角色、权限变更后立即生效，无需重新登录
				 * 2、每次请求都会重新从数据库/缓存中构建 ClaimsPrincipal，而不是完全依赖 Token 里的内容
				 */
				options.IsDynamicClaimsEnabled = true;
			});
		}

		private void ConfigureUrls(IConfiguration configuration)
		{
			Configure<AppUrlOptions>(options =>
			{
				// 设置 MVC 应用的根 URL
				options.Applications["MVC"].RootUrl = configuration["App:SelfUrl"];
				// 配置重定向 URL 白名单，防止重定向到名单以外的钓鱼网站
				options.RedirectAllowedUrls.AddRange(configuration["App:RedirectAllowedUrls"]?.Split(',') ?? Array.Empty<string>());
			});
		}

		private void ConfigureBundles(IHostEnvironment hostingEnvironment)
		{
			Configure<AbpBundlingOptions>(options =>
			{
				/*
				 * 配置全局样式包
				 * 将项目根目录下的 /global-styles.css 加入到主题的全局样式包中
				 * 所有使用该主题的页面都会自动加载这个 CSS，无需手动在布局页中引用
				 */
				options.StyleBundles.Configure(
					LeptonXLiteThemeBundles.Styles.Global,
					bundle =>
					{
						bundle.AddFiles("/global-styles.css");
					}
				);

				/*
				 * 配置全局脚本包
				 * /global-scripts.js：所有环境都会加载，用于放置自定义前端逻辑
				 * /dev-login-helper.js：仅在开发环境加载
				 */
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
				// 虚拟文件系统，由于ABP中的.cshtml、.js、.css 等资源文件是嵌套在DLL中，修改后，需要重新编译才能生效；
				// ReplaceEmbeddedByPhysical —— 在开发环境下，告诉 ABP："不要从 DLL 里读这些文件，去磁盘上的源码目录读。"
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
				// 根据应用服务自动生成RESTful风格的API
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

		/// <summary>
		/// 关闭 ABP MVC 主题的客户端资源（libs）检查。
		/// 本项目 UI 是 Vue SPA，不需要 bootstrap/jquery/select2 这些资源；
		/// 若不关闭，所有请求（含 /api 与 /connect/token）都会被 "The Libs Folder is Missing" 错误页拦下，
		/// 而 <c>abp install-libs</c> 又要求 node ≥ 24（本机 node 22 被 select2 的引擎校验拦住）。
		/// </summary>
		private void ConfigureMvcLibs()
		{
			Configure<AbpMvcLibsOptions>(options =>
			{
				options.CheckLibs = false;
			});
		}

		public override void OnApplicationInitialization(ApplicationInitializationContext context)
		{
			var app = context.GetApplicationBuilder();
			var env = context.GetEnvironment();

			// 转发头处理，确保程序拿到客户端真实IP
			app.UseForwardedHeaders();

			if (env.IsDevelopment())
			{
				// 发生异常时，显示详细的堆栈跟踪和源码片段
				app.UseDeveloperExceptionPage();
			}

			// 根据请求头 Accept-Language 或 URL 参数，自动切换语言
			app.UseAbpRequestLocalization();

			if (!env.IsDevelopment())
			{
				// 生产环境下，发生未处理异常时显示友好的错误页面（不含堆栈信息）
				app.UseErrorPage();
			}

			// 解析请求 URL，匹配到对应的端点（Endpoint），但不执行端点
			app.UseRouting();

			// 映射 ABP 的静态资源（如 LeptonXLite 主题的 css/js）
			app.MapAbpStaticAssets();

			// ABP Studio 链路追踪（前面 IsLinkEnabled = false 已在生产禁用）
			app.UseAbpStudioLink();

			// 添加安全响应头（X-Frame-Options、X-Content-Type-Options 等）
			app.UseAbpSecurityHeaders();
			app.UseCors();

			// SignalR 的 WebSocket 握手带不了自定义头，认证前先把 query string 里的令牌搬进 Authorization 头
			app.UseMiddleware<SignalRAccessTokenMiddleware>();

			// 解析请求中的凭证（Bearer Token、Cookie），构建 HttpContext.User
			app.UseAuthentication();

			// ABP 封装的 OpenIddict 验证中间件，验证 JWT Token 的签名、有效期、受众
			app.UseAbpOpenIddictValidation();

			if (MultiTenancyConsts.IsEnabled)
			{
				// 从请求中解析租户标识（如 __tenant 请求头或子域名），切换当前租户上下文
				app.UseMultiTenancy();
			}

			// 开启工作单元，请求结束时自动提交/回滚数据库事务
			app.UseUnitOfWork();

			// 配合前面的 IsDynamicClaimsEnabled = true，动态构建最新的 Claims（权限实时生效）
			app.UseDynamicClaims();

			// 检查当前用户是否有权限访问匹配的端点，无权限则返回 401/403
			app.UseAuthorization();

			app.UseSwagger();
			app.UseAbpSwaggerUI(options =>
			{
				options.SwaggerEndpoint("/swagger/v1/swagger.json", "KnowVault API");

				var configuration = context.ServiceProvider.GetRequiredService<IConfiguration>();
				options.OAuthClientId(configuration["AuthServer:SwaggerClientId"]);
			});

			// 记录审计日志（谁在什么时候调用了哪个 API，参数和结果是什么）
			app.UseAuditing();

			// 给 Serilog 日志添加上下文信息（如当前用户、租户、请求 ID）
			app.UseAbpSerilogEnrichers();
			app.UseConfiguredEndpoints(endpoints => endpoints.MapHub<QaHub>(QaHub.Route));
		}
	}
}
