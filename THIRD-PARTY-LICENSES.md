# 第三方组件与许可

KnowVault 自身以 [MIT](LICENSE) 许可发布。本文件列出仓库直接使用的第三方代码依赖与容器服务镜像，便于合规审查。

> 说明：下表覆盖**直接依赖**。传递依赖可通过 `dotnet list package --include-transitive` 与 `npm ls --all` 复核；所有 NuGet / npm 包均以动态链接或运行时加载方式使用，仓库未修改其源码。

## 一、后端 NuGet 包

| 组件 | 版本 | 许可 |
|---|---|---|
| ABP Framework（Volo.Abp.*） | 10.5.0 | LGPL-3.0 |
| ABP LeptonXLite 主题 | 5.5.0 | LGPL-3.0 |
| ABP Studio Client | 3.0.8 | LGPL-3.0 |
| Microsoft Semantic Kernel | 1.80.0 | MIT |
| Microsoft Semantic Kernel Connectors OpenAI | 1.80.0 | MIT |
| OpenAI .NET SDK | 2.10.0 | MIT |
| MassTransit / MassTransit.RabbitMQ | 8.5.7 | Apache-2.0 |
| Qdrant.Client | 1.19.0 | Apache-2.0 |
| Minio | 6.0.5 | Apache-2.0 |
| PdfPig | 0.1.16 | Apache-2.0 |
| DocumentFormat.OpenXml | 3.5.1 | MIT |
| ClosedXML | 0.105.1 | MIT |
| Markdig | 1.4.0 | BSD-2-Clause |
| HtmlAgilityPack | 1.13.0 | MIT |
| IdentityModel | 7.0.0 | Apache-2.0 |
| KubernetesClient | 18.0.5 | Apache-2.0 |
| AspNetCore.HealthChecks.UI / Client / InMemory.Storage | 9.0.0 | Apache-2.0 |
| Serilog.AspNetCore | 9.0.0 | Apache-2.0 |
| Serilog.Sinks.Async | 2.1.0 | Apache-2.0 |
| Serilog.Sinks.Seq | 9.1.0 | Apache-2.0 |
| OpenTelemetry.* | 1.20.0 | Apache-2.0 |
| OpenTelemetry.Instrumentation.EntityFrameworkCore | 1.17.0-beta.1 | Apache-2.0 |
| Microsoft.Extensions.* / Microsoft.EntityFrameworkCore.* | 10.0.9 | MIT |
| Microsoft.AspNetCore.Mvc.Razor.RuntimeCompilation | 10.0.9 | MIT |
| Aspire.Hosting.*（本地编排） | 13.5.3 | MIT |

## 二、测试与工具

| 组件 | 版本 | 许可 |
|---|---|---|
| xUnit | 2.9.3 | Apache-2.0 |
| xunit.runner.visualstudio | 2.8.2 | Apache-2.0 |
| Microsoft.NET.Test.Sdk | 17.14.1 | MIT |
| Testcontainers（及 Minio/PostgreSql/RabbitMq 模块） | 4.16.0 / 4.15.0 | MIT |
| FluentAssertions | 7.2.0 | Apache-2.0 |
| coverlet.collector | 6.0.4 | MIT |

> FluentAssertions 固定 7.x：8.x 起改为商业许可，本项目不使用。

## 三、前端 npm 包

| 组件 | 版本 | 许可 |
|---|---|---|
| Vue | ^3.5.12 | MIT |
| Pinia | ^2.2.6 | MIT |
| Naive UI | ^2.40.1 | MIT |
| Axios | ^1.7.7 | MIT |
| markdown-it | ^14.3.3 | MIT |
| ECharts | ^5.5.1 | Apache-2.0 |
| @microsoft/signalr | ^10.0.11 | MIT |
| Vite | ^6.0.3 | MIT |
| @vitejs/plugin-vue | ^5.1.4 | MIT |

## 四、容器服务镜像

| 服务 | 镜像 | 许可 |
|---|---|---|
| PostgreSQL | `postgres:16` | PostgreSQL License |
| Redis | `redis:8.6` | RSALv2 / SSPLv1 / AGPLv3（三选一） |
| Qdrant | `qdrant/qdrant:v1.15.0` | Apache-2.0 |
| RabbitMQ | `rabbitmq:3-management` | MPL-2.0 |
| MinIO | `minio/minio:RELEASE.2025-09-07T16-13-09Z` | AGPL-3.0 |
| Seq | `datalust/seq:2025.1` | 商业许可（单用户免费，以官方条款为准） |
| .NET 运行时 | `mcr.microsoft.com/dotnet/*:10.0` | MIT |
| nginx | `nginx:1.27-alpine` | BSD-2-Clause |

## 五、许可口径

- 仓库**代码依赖**中没有 AGPL / SSPL 组件。MinIO（AGPL-3.0）与 Redis（RSALv2 / SSPLv1 / AGPLv3 三选一）以**未修改的官方容器镜像**运行，通过 HTTP / TCP 与业务代码交互，不与仓库代码静态链接，因此不改变 KnowVault 的 MIT 许可。
- ABP Framework 以 NuGet 动态链接方式使用，仓库未修改其源码；LGPL-3.0 允许闭源或开源项目链接使用，保留其版权与许可声明即可。
- Seq 为商业软件，Compose 只拉取官方镜像、不重新分发；本地演示属于单用户免费范围。
- 如要求「运行服务也不能使用 AGPL/SSPL」，可将 Redis 换为 Valkey、MinIO 换为 SeaweedFS；该替换不在当前交付范围内。

## 六、复核命令

```bash
dotnet list KnowVault.slnx package --include-transitive
cd frontend && npm ls --all
```
