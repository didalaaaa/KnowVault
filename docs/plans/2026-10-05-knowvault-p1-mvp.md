# KnowVault P1「最小闭环」实施计划

> **执行方式**：逐任务执行，每个任务以「构建/测试 + 提交」收尾；执行纪律见 `executing-plans` 技能。
> **需求规格已于 2026-10-05 冻结**（见 §0）。执行中若必须偏离规格，**先改 §0 再改代码**，禁止静默偏离。
> **动手前的铁律**（来自 `AGENTS.md`）：改文件前先 `read`；行尾整文件一致（新文件 CRLF）；注释中文；枚举名必须带 `Enum` 后缀；扁平根命名空间（`namespace KnowVault.Xxx`，块级声明）。

**Goal:** 打通「上传 → 解析 → 切片 → 向量化 → Qdrant 写入 → 提问 → 检索 → DeepSeek → 引用溯源」的最小问答闭环，并让 Vue 前端可登录、可上传、可看到文档状态与引用，形成可 demo 的 MVP。

**Architecture:** 单宿主进程内闭环。`KnowVault.HttpApi.Host` 承载 ABP 应用层 + MassTransit RabbitMQ consumer（进程内消费，队列仍走 RabbitMQ，保持宿主无关以便 P3 拆独立 Worker）。`KnowVault.Ingest` 负责解析/切片，`KnowVault.Ai` 负责 embedding/检索/生成，`KnowVault.EntityFrameworkCore` 承担 Infrastructure 角色（Qdrant 与 MinIO 的实现放这里，**接口定义在 `KnowVault.Domain`**）。

**Tech Stack（版本已核实存在且许可干净）:**

| 用途 | 包 | 版本 | 许可 |
|---|---|---|---|
| 消息 | `MassTransit` / `MassTransit.RabbitMQ` | **8.5.7** | Apache-2.0（**9.x 是 Massient 商业许可，禁止使用**） |
| 向量库 | `Qdrant.Client` | 1.19.0 | Apache-2.0 |
| 对象存储 | `Minio` | 6.0.5 | Apache-2.0 |
| PDF | `PdfPig` | 0.1.16 | Apache-2.0 |
| Word | `DocumentFormat.OpenXml` | 3.5.1 | MIT |
| Excel | `ClosedXML` | 0.105.1 | MIT |
| Markdown | `Markdig` | 1.4.0 | BSD-2-Clause |
| HTML | `HtmlAgilityPack` | 1.13.0 | MIT |
| 生成 | `Microsoft.SemanticKernel(.Connectors.OpenAI)` | 1.80.0 | MIT（已引用） |
| 单测 | `xunit` 2.9.3 + `xunit.runner.visualstudio` 2.8.2 + `Microsoft.NET.Test.Sdk` 17.14.1 | — | Apache-2.0 |

---

## §0 冻结的需求规格（2026-10-05，用户逐项确认）

### 0.1 六项决策

| # | 决策 |
|---|---|
| 1 | **Ingest 拓扑**：进程内 MassTransit consumer（Host 内），**仍走 RabbitMQ**；consumer/processor 写在 `KnowVault.Ingest` 且宿主无关 |
| 2 | **认证范围**：完整 SPA 登录 —— Vue 登录页 + Resource Owner Password grant（复用已种子的 `KnowVault_App`）+ axios 注入 Bearer + API 加 `[Authorize]` |
| 3 | **格式范围**：PDF / Word(.docx) / Excel(.xlsx) / Markdown / **上传 .html 文件**；**不做 URL 抓取** |
| 4 | **模型**：DeepSeek + Qwen 双 Key 就绪，**端到端真跑**，不做 Mock provider |
| 5 | **向量库接缝**：接口 `IVectorStore` 放 `KnowVault.Domain`，实现放 `KnowVault.EntityFrameworkCore`，客户端用官方 `Qdrant.Client` |
| 6 | **知识库**：DbMigrator 种子「默认知识库」+ 最小 KB 列表 API + 前端下拉默认选中 |

### 0.2 默认项（用户未反对，按此执行）

- **D1** MinIO：引 Minio SDK 自封装 `IDocumentStorage`，不用 ABP BlobStoring 的 S3 兼容 provider。
- **D2** Excel：`ClosedXML`（仅 `.xlsx`），弃 NPOI。
- **D3** chunk/overlap **以字符数为单位近似 token**（默认 1000 / 120，≈600 / 70 token 量级）；中文场景没有可靠 .NET tokenizer，不引 tiktoken 类依赖。
- **D4** `DocumentChunk` 表**不加字段**（省一次 EF 迁移）；`headingPath` / `documentTitle` 只进 Qdrant payload。
- **D5** 单文件 ≤ 50MB；不做去重 / 删除 / 自动重试；前端上传后 2s 轮询文档列表。

### 0.3 风险与已核实结论

- **R1 MassTransit 版本**：9.2.3 的 nuspec 无 license 表达式、只有 `licenseUrl=https://massient.com/license`（Massient 商业产品）；8.5.7 为 `Apache-2.0` 且带 `net10.0` 目标组 → **只用 8.5.7**。
- **R2 Aspire 动态端口 vs OpenIddict issuer**：`aspire/KnowVault.AppHost` 无 `launchSettings.json`，API 端口由 Aspire 动态分配；而 `AuthServer:Authority` = `https://localhost:44300`，issuer 不一致会导致 token 校验失败 → **AppHost 必须钉住 44300**。
- **R3 多租户已启用**：`MultiTenancyConsts.IsEnabled = true` → `TenantId` 可能为 null，而 Qdrant 无法对 null 做 match → payload 中**归一化为 `TenantId ?? Guid.Empty`**。
- **R4 MinIO 是 AGPL-3.0（服务端）**：仓库只通过 Docker 运行**未修改的官方镜像**，调用方是我们自己 Apache-2.0 的 SDK，不构成对仓库代码的许可传染；若要彻底规避可换 S3 兼容替代品。此为**记录在案**，不阻塞 P1。
- **R5 FluentAssertions**：7.2.0 是 Apache-2.0，8.11.0 已改为**许可文件（Xceed 商业 EULA）**。因此 P1 单测**只用 xUnit 内置 `Assert`**，不引入 FluentAssertions；`TECH_STACK.md` 中的 FluentAssertions 需注明"必须钉 7.x"。

### 0.4 非目标（P1 明确不做）

Rerank、多轮对话历史加载、混合检索(BM25)、知识库管理界面、角色/知识库级权限细分、OCR、URL 抓取、流式输出(SignalR)、OpenTelemetry/Seq 全链路、Testcontainers 集成测试、文档删除与 Qdrant points 清理、失败自动重试与"重新处理"按钮、同名去重。

### 0.5 关于测试的偏差声明（需你默许）

`writing-plans` 规范要求 TDD。但 `TECH_STACK.md` §6 把 `tests/`（Testcontainers 集成测试）划在 **P3**。折中方案（本计划采用）：

- 新增**一个**测试项目 `tests/KnowVault.Tests`，**只测纯函数**：切片器、解析器（Markdown/HTML/Word/Excel）、Qdrant payload 映射与读取、Prompt 组装；
- **不引入** Testcontainers / 数据库 / 网络依赖；PDF 解析器因缺少可复现的二进制夹具，改为**手工验收**（见 Task 14），P3 再补夹具约定；
- 若你要求严格把 `tests/` 留到 P3，则把这些步骤降级为"手工验证"，任务结构不变。

---

## §1 任务总览与依赖顺序

| Task | 内容 | 依赖 | 主要产出 |
|---|---|---|---|
| 0 | 文档与配置契约同步 | — | TECH_STACK/AGENTS/.env.example 与本计划 |
| 1 | 配置选项类 + 绑定 | 0 | `IngestOptions` / `QdrantOptions` / `MinioOptions` + Ai 节 |
| 2 | 测试项目落地 | 0 | `tests/KnowVault.Tests` |
| 3 | Ai 层：Qwen embedding + DeepSeek 生成 | 1, 2 | `IEmbeddingService` / `ILlmChatService` |
| 4 | 向量库抽象 + Qdrant 实现 | 1, 2 | `IVectorStore` / `QdrantVectorStore` / `ChunkPayload` |
| 5 | 文档存储（MinIO） | 1, 2 | `IDocumentStorage` / `MinioDocumentStorage` |
| 6 | 5 种格式解析器 | 1, 2 | `IDocumentParser` + 5 个实现 |
| 7 | 切片器（核心算法，TDD） | 2 | `IDocumentChunker` / `DocumentChunker` |
| 8 | Ingest 链路 + MassTransit 消费 | 3, 4, 5, 6, 7 | `DocumentProcessor` 真实现 + consumer |
| 9 | 上传/KB API + 默认知识库种子 | 5, 8 | `UploadAsync` / `KnowledgeBaseAppService` / Seeder |
| 10 | RagService 真实现 + 引用溯源 | 3, 4 | `RagPromptBuilder` / `RagService` |
| 11 | Host/AppHost 装配（端口、Key、引用） | 4, 5, 8 | 可启动的全栈环境 |
| 12 | 前端登录 + Bearer | 11 | 登录页 / token 注入 / 401 处理 |
| 13 | 前端上传 + 文档状态 + KB 下拉 | 9, 12 | 可 demo 的界面 |
| 14 | 端到端验收 + 负向验证 + 收尾 | 全部 | 验收记录 + 文档回写 |

**每个任务的收尾动作固定为**：

```powershell
dotnet build KnowVault.slnx
dotnet test  tests\KnowVault.Tests\KnowVault.Tests.csproj   # Task 2 之后
dotnet format style --verify-no-changes --diagnostics IDE1006 --project KnowVault.slnx
git -C . add -A; git -C . commit -m "<type>: <subject>"
```

---

## Task 0：文档与配置契约同步

**Files:**
- Modify: `TECH_STACK.md`（§1 技术栈表、§3 结构注释、§8 开放项、§9 定稿、新增 §10 计划引用）
- Modify: `AGENTS.md`（§一 技术底座表、§四 数据与 AI 层约定、§七 待办）
- Modify: `.env.example`（说明 AppHost 的变量名映射）
- Create: `docs/plans/2026-10-05-knowvault-p1-mvp.md`（本文件）

**Step 1：改 `TECH_STACK.md` 的 5 处**

1. §1 表格「异步队列」行 → `**RabbitMQ** / **MassTransit 8.5.7（Apache-2.0）**（9.x 为 Massient 商业许可，勿用）`
2. §3 结构注释中 `KnowVault.EntityFrameworkCore` 行 → `// EF Core(PostgreSQL) + DbContext / EF 迁移；Qdrant 与 MinIO 访问实现（接口定义在 KnowVault.Domain）`
3. §8 开放项第 1 条 → `- [x] DeepSeek / Qwen 的 Key 已确认会配置（P1 端到端真跑；Key 走环境变量 \`Ai__*\`，不进仓库）`
4. §9 追加 P1 定稿条目（原文保留）：

```markdown
### 9.1 P1 实现细节定稿（2026-10-05）

- **消息**：`MassTransit 8.5.7`（Apache-2.0）+ `MassTransit.RabbitMQ`；**进程内 consumer**（Host 内消费，队列仍走 RabbitMQ），consumer/processor 写在 `KnowVault.Ingest` 且保持宿主无关，P3 可平移为独立 Worker
- **向量库**：官方 `Qdrant.Client 1.19`；接口 `IVectorStore` 在 `KnowVault.Domain`，实现 `QdrantVectorStore` 在 `KnowVault.EntityFrameworkCore`；单一 collection `knowvault_chunks`，1024 维（`text-embedding-v3`）、Cosine
- **payload 字段**：`tenantId / knowledgeBaseId / documentId / chunkIndex / content / documentTitle / headingPath / sourceType`；**`tenantId` 为 null 时归一化为 `Guid.Empty`**（Qdrant 无法 match null）；pointId = `DocumentChunk.Id`（UUID）
- **对象存储**：`Minio` SDK 自封装 `IDocumentStorage`（接口在 Domain，实现在 EntityFrameworkCore），bucket `knowvault`，对象键 `{tenantId|host}/{knowledgeBaseId}/{documentId}{ext}`；不用 ABP BlobStoring 的 S3 兼容 provider
- **解析库**：PDF=`PdfPig`、Word=`.docx`→`DocumentFormat.OpenXml`、Excel=`.xlsx`→**`ClosedXML`（弃 NPOI）**、Markdown=`Markdig`、HTML=`HtmlAgilityPack`
- **切片单位**：以**字符数近似 token**（默认 1000 字符 / overlap 120）——中文没有可靠 .NET tokenizer，不引 tiktoken 类依赖
- **P1 不做**：rerank、多轮历史、删除文档、失败自动重试、URL 抓取、权限细分
- **测试**：P1 只加 `tests/KnowVault.Tests` 的**纯函数单测**（切片/解析/payload/Prompt），Testcontainers 集成测试仍留 P3；FluentAssertions 若要引入**必须钉 7.x（Apache-2.0）**
- **执行计划**：见 `docs/plans/2026-10-05-knowvault-p1-mvp.md`
```

5. 新增 §10 引用本计划文件路径。

**Step 2：改 `AGENTS.md` 的 3 处（注意：该文件是 LF 例外，改完必须仍是纯 LF）**

1. §一 技术底座表「异步」行 → `RabbitMQ / MassTransit 8.5.7（Apache-2.0；9.x 商业许可勿用）`
2. §四 红线「文档解析库」行 → `Excel=.xlsx → ClosedXML`（并把 `NPOI/ClosedXML` 的写法删掉）；同节追加一句：
   `- **Qdrant 单一 collection `knowvault_chunks`**，向量 1024 维 Cosine；payload 的 `tenantId` 在租户为 null 时写 `Guid.Empty`（Qdrant 不支持 match null）。`
3. §七 待办 → 更新为：Key 已确认会配（`Ai__*` 环境变量）；`tests/` 在 P1 先落「纯函数单测项目 `tests/KnowVault.Tests`」，Testcontainers 集成测试仍按 §6 留 P3。

**Step 3：改 `.env.example`**

保持 `DEEPSEEK_API_KEY` / `QWEN_API_KEY` 这类人读友好的名字不变，但**补上映射说明**（AppHost 会把它映射成后端读取的 `Ai__DeepSeekApiKey`）：

```dotenv
# 后端实际读取的配置键是 Ai:DeepSeekApiKey / Ai:QwenApiKey（即环境变量 Ai__DeepSeekApiKey / Ai__QwenApiKey）。
# 为便于阅读，本文件用短名；aspire/KnowVault.AppHost 启动时会做映射（见 Task 11 的 Env(...) 映射代码）。
```

**Step 4：行尾自查（两个数字必须有一个为 0）**

```powershell
foreach ($p in @('TECH_STACK.md','AGENTS.md','.env.example','docs/plans/2026-10-05-knowvault-p1-mvp.md')) {
  $t=[IO.File]::ReadAllText("$p")
  "$p CRLF=$(([regex]::Matches($t,"`r`n")).Count) loneLF=$(([regex]::Matches($t,"(?<!`r)`n")).Count)"
}
```

期望：`TECH_STACK.md` / `.env.example` / 计划文件 **loneLF=0**；`AGENTS.md` **CRLF=0**（它是文档化的 LF 例外）。
（`.gitattributes` 为 `* text=auto eol=crlf`，把 `TECH_STACK.md` 归一化成 CRLF **不会产生 git diff 噪音**。）

**Step 5：提交**

```powershell
git -C . add -A
git -C . commit -m "docs: 冻结 P1 需求规格与实施计划，同步 MassTransit/MinIO/Qdrant 定稿"
```

---

## Task 1：配置选项类与绑定

**Files:**
- Create: `src/KnowVault.Ingest/Configuration/IngestOptions.cs`
- Create: `src/KnowVault.EntityFrameworkCore/Configuration/QdrantOptions.cs`
- Create: `src/KnowVault.EntityFrameworkCore/Configuration/MinioOptions.cs`
- Modify: `src/KnowVault.Ingest/KnowVaultIngestModule.cs`（绑定 `Ingest` 节）
- Modify: `src/KnowVault.EntityFrameworkCore/EntityFrameworkCore/KnowVaultEntityFrameworkCoreModule.cs`（绑定 `Qdrant` / `Minio` 节）
- Modify: `src/KnowVault.HttpApi.Host/appsettings.json`（新增 `Ai` / `Ingest` / `Qdrant` / `Minio` 节）
- Modify: `src/KnowVault.Ai/KnowVault.Ai.csproj`（补 `Microsoft.Extensions.Http`，Task 3 需要 `IHttpClientFactory`）

**Step 1：写 `IngestOptions.cs`**

```csharp
using KnowVault.Domain.Shared;

namespace KnowVault.Ingest
{
	/// <summary>
	/// 文档摄入参数（P1 走配置绑定；ABP Setting 化留二期）。
	/// </summary>
	public class IngestOptions
	{
		/// <summary>
		/// 单个切片的目标长度（单位：字符）。
		/// 中文场景没有可靠 tokenizer，用字符近似 token：约 1 token ≈ 1.6 中文字符。
		/// </summary>
		public int ChunkSizeChars { get; set; } = 1000;

		/// <summary>相邻切片的重叠长度（字符），避免语义被切断。</summary>
		public int ChunkOverlapChars { get; set; } = 120;

		/// <summary>允许上传的扩展名白名单（小写、含点）。</summary>
		public string[] AllowedExtensions { get; set; } =
			[".pdf", ".docx", ".xlsx", ".md", ".markdown", ".html", ".htm"];

		/// <summary>单文件大小上限（MB）。</summary>
		public int MaxFileSizeMb { get; set; } = 50;

		/// <summary>按扩展名推断来源类型；未知扩展名返回 null。</summary>
		/// <param name="extension">含点的小写扩展名，例如 <c>.docx</c>。</param>
		/// <returns>对应的来源类型；无法识别时为 <c>null</c>。</returns>
		public static DocumentSourceTypeEnum? ResolveSourceType(string extension) => extension switch
		{
			".pdf" => DocumentSourceTypeEnum.Pdf,
			".docx" => DocumentSourceTypeEnum.Word,
			".xlsx" => DocumentSourceTypeEnum.Excel,
			".md" or ".markdown" => DocumentSourceTypeEnum.Markdown,
			".html" or ".htm" => DocumentSourceTypeEnum.Html,
			_ => null
		};
	}
}
```

**Step 2：写 `QdrantOptions.cs`**

```csharp
namespace KnowVault.EntityFrameworkCore
{
	/// <summary>Qdrant 连接与 collection 参数。</summary>
	public class QdrantOptions
	{
		/// <summary>gRPC 端点（注意不是 6333 的 REST 端口，而是 6334）。</summary>
		public string Endpoint { get; set; } = "http://localhost:6334";

		/// <summary>向量维度，必须与 embedding 模型输出一致（text-embedding-v3 = 1024）。</summary>
		public ulong VectorSize { get; set; } = 1024;

		/// <summary>单一 collection 名（禁止按知识库拆 collection）。</summary>
		public string CollectionName { get; set; } = "knowvault_chunks";

		/// <summary>可选的 API Key（自托管默认无）。</summary>
		public string? ApiKey { get; set; }
	}
}
```

**Step 3：写 `MinioOptions.cs`**

```csharp
namespace KnowVault.EntityFrameworkCore
{
	/// <summary>MinIO 连接参数。密钥一律来自环境变量/AppHost 注入，不在代码里写默认值。</summary>
	public class MinioOptions
	{
		/// <summary>形如 <c>localhost:9000</c>（不带协议）。</summary>
		public string Endpoint { get; set; } = string.Empty;

		public string AccessKey { get; set; } = string.Empty;

		public string SecretKey { get; set; } = string.Empty;

		/// <summary>本地开发为 false（容器未启 TLS）。</summary>
		public bool UseSsl { get; set; }

		/// <summary>存放原始文档的 bucket，代码启动时会幂等创建。</summary>
		public string BucketName { get; set; } = "knowvault";
	}
}
```

**Step 4：模块里绑定配置**

`KnowVaultIngestModule.ConfigureServices` 增加：

```csharp
var configuration = context.Services.GetConfiguration();
Configure<IngestOptions>(configuration.GetSection("Ingest"));
```

`KnowVaultEntityFrameworkCoreModule.ConfigureServices` 增加：

```csharp
var configuration = context.Services.GetConfiguration();
Configure<QdrantOptions>(configuration.GetSection("Qdrant"));
Configure<MinioOptions>(configuration.GetSection("Minio"));
```

**Step 5：`appsettings.json` 新增节（Key 一律留空，靠环境变量注入）**

```json
"Ai": {
  "DeepSeekApiKey": "",
  "DeepSeekBaseUrl": "https://api.deepseek.com",
  "DeepSeekModel": "deepseek-chat",
  "QwenApiKey": "",
  "QwenBaseUrl": "https://dashscope.aliyuncs.com/compatible-mode/v1",
  "QwenEmbeddingModel": "text-embedding-v3",
  "QwenRerankModel": "gte-rerank",
  "TopK": 5
},
"Ingest": {
  "ChunkSizeChars": 1000,
  "ChunkOverlapChars": 120,
  "MaxFileSizeMb": 50
},
"Qdrant": {
  "Endpoint": "http://localhost:6334",
  "VectorSize": 1024,
  "CollectionName": "knowvault_chunks"
},
"Minio": {
  "BucketName": "knowvault",
  "UseSsl": false
}
```

**Step 6：验证**

```powershell
dotnet build KnowVault.slnx
```

期望：0 error（新选项类不参与任何调用，仅确认编译与绑定键路径正确）。

**Step 7：提交** `feat: 增加 Ingest/Qdrant/MinIO 配置选项与 AI 配置节`

---

## Task 2：测试项目落地

**Files:**
- Create: `tests/KnowVault.Tests/KnowVault.Tests.csproj`
- Modify: `KnowVault.slnx`（新增 `/tests/` 文件夹与项目）

**Step 1：写 csproj**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <Import Project="..\..\common.props" />

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\KnowVault.Domain\KnowVault.Domain.csproj" />
    <ProjectReference Include="..\..\src\KnowVault.Ai\KnowVault.Ai.csproj" />
    <ProjectReference Include="..\..\src\KnowVault.Ingest\KnowVault.Ingest.csproj" />
    <ProjectReference Include="..\..\src\KnowVault.EntityFrameworkCore\KnowVault.EntityFrameworkCore.csproj" />
  </ItemGroup>

</Project>
```

> 说明：单项目引用全部被测层，避免 P1 就摊开 4 个测试工程；P3 引入 Testcontainers 时再按需拆分。
> **不引 FluentAssertions**（8.x 已非 OSS，见 §0.3 R5），一律用 xUnit 内置 `Assert`。

**Step 2：加进 `KnowVault.slnx`**

```xml
    <Folder Name="/tests/">
        <Project Path="tests/KnowVault.Tests/KnowVault.Tests.csproj" />
    </Folder>
```

**Step 3：放一个占位测试确认基建可用**

`tests/KnowVault.Tests/SmokeTests.cs`（Task 3 起会被真实测试取代/保留）：

```csharp
namespace KnowVault.Tests
{
	/// <summary>确认测试基建可跑通的占位用例。</summary>
	public class SmokeTests
	{
		/// <summary>最简单的断言，用于验证测试工程被正确发现与执行。</summary>
		[Fact]
		public void Test_infrastructure_works() => Assert.True(true);
	}
}
```

（`Fact` 来自 `Xunit`，`ImplicitUsings` 不含它，需要在文件顶部加 `using Xunit;`。）

**Step 4：验证**

```powershell
dotnet test tests\KnowVault.Tests\KnowVault.Tests.csproj
```

期望：`Passed! - Failed: 0, Passed: 1`。

**Step 5：提交** `test: 增加 KnowVault.Tests 纯函数单测工程`

---

## Task 3：Ai 层 —— Qwen Embedding 与 DeepSeek 生成

**Files:**
- Create: `src/KnowVault.Ai/Embeddings/IEmbeddingService.cs`
- Create: `src/KnowVault.Ai/Embeddings/QwenEmbeddingService.cs`
- Create: `src/KnowVault.Ai/Chat/ILlmChatService.cs`
- Create: `src/KnowVault.Ai/Chat/DeepSeekChatService.cs`
- Create: `src/KnowVault.Ai/AiCallException.cs`
- Modify: `src/KnowVault.Ai/KnowVaultAiModule.cs`
- Modify: `src/KnowVault.Ai/KnowVault.Ai.csproj`

**Step 1：契约**

```csharp
namespace KnowVault.Ai
{
	/// <summary>文本向量化（Embedding）。</summary>
	public interface IEmbeddingService
	{
		/// <summary>把一批文本转成向量，返回顺序与输入严格一致。</summary>
		/// <param name="texts">待向量化的文本（不得为空字符串）。</param>
		/// <param name="cancellationToken">取消令牌。</param>
		/// <returns>与 <paramref name="texts"/> 等长的向量数组。</returns>
		Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default);
	}
}
```

```csharp
namespace KnowVault.Ai
{
	/// <summary>对话式文本生成（P1 只需一次性补全；流式留 P2）。</summary>
	public interface ILlmChatService
	{
		/// <summary>按 system + user 组装并生成回答。</summary>
		/// <param name="systemPrompt">系统提示词（强约束回答范围）。</param>
		/// <param name="userPrompt">用户提示词（含检索片段与问题）。</param>
		/// <param name="cancellationToken">取消令牌。</param>
		/// <returns>模型返回的纯文本回答。</returns>
		Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default);
	}
}
```

```csharp
namespace KnowVault.Ai
{
	/// <summary>调用外部模型服务失败（HTTP 非 2xx、响应结构异常等）。</summary>
	public class AiCallException : Exception
	{
		/// <summary>用错误信息构造。</summary>
		/// <param name="message">错误详情（含 HTTP 状态码与服务端返回体，便于排查）。</param>
		public AiCallException(string message) : base(message)
		{
		}

		/// <summary>用错误信息与内部异常构造。</summary>
		/// <param name="message">错误详情。</param>
		/// <param name="innerException">内部异常。</param>
		public AiCallException(string message, Exception innerException) : base(message, innerException)
		{
		}
	}
}
```

**Step 2：`QwenEmbeddingService`（走 DashScope 的 OpenAI 兼容 `/embeddings`）**

要点：**必须自己分批**（兼容模式单请求文本数有上限，实测确定，先按 10 保守处理）；**必须按响应里的 `index` 重排**，否则向量与文本错位（这是最容易埋进去的脏 bug）。

```csharp
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace KnowVault.Ai
{
	/// <summary>
	/// Qwen（阿里云百炼 OpenAI 兼容模式）Embedding 实现。
	/// 兼容模式对单请求文本数有上限，这里按 <see cref="MaxBatchSize"/> 分批，并按响应 index 还原顺序。
	/// </summary>
	public class QwenEmbeddingService : IEmbeddingService, ITransientDependency
	{
		// 保守批大小：兼容模式上限以实测为准，超限会返回 400，宁可多跑几批
		private const int MaxBatchSize = 10;

		private readonly AiOptions _options;
		private readonly IHttpClientFactory _httpClientFactory;
		private readonly ILogger<QwenEmbeddingService> _logger;

		/// <summary>构造函数。</summary>
		/// <param name="options">AI 配置（模型名、BaseUrl、Key）。</param>
		/// <param name="httpClientFactory">HTTP 客户端工厂。</param>
		/// <param name="logger">日志。</param>
		public QwenEmbeddingService(
			IOptions<AiOptions> options,
			IHttpClientFactory httpClientFactory,
			ILogger<QwenEmbeddingService> logger)
		{
			_options = options.Value;
			_httpClientFactory = httpClientFactory;
			_logger = logger;
		}

		/// <inheritdoc />
		public async Task<IReadOnlyList<float[]>> EmbedAsync(
			IReadOnlyList<string> texts,
			CancellationToken cancellationToken = default)
		{
			if (texts.Count == 0)
			{
				return Array.Empty<float[]>();
			}

			EnsureConfigured();

			var all = new List<float[]>(texts.Count);
			foreach (var batch in texts.Chunk(MaxBatchSize))
			{
				all.AddRange(await EmbedBatchAsync(batch, cancellationToken));
			}

			return all;
		}

		private async Task<List<float[]>> EmbedBatchAsync(string[] batch, CancellationToken cancellationToken)
		{
			var client = _httpClientFactory.CreateClient(nameof(QwenEmbeddingService));
			using var request = new HttpRequestMessage(
				HttpMethod.Post,
				$"{_options.QwenBaseUrl.TrimEnd('/')}/embeddings");
			request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.QwenApiKey);
			request.Content = JsonContent.Create(new
			{
				model = _options.QwenEmbeddingModel,
				input = batch,
				encoding_format = "float"
			});

			using var response = await client.SendAsync(request, cancellationToken);
			var body = await response.Content.ReadAsStringAsync(cancellationToken);
			if (!response.IsSuccessStatusCode)
			{
				throw new AiCallException($"Qwen embedding 调用失败：HTTP {(int)response.StatusCode} {body}");
			}

			using var document = JsonDocument.Parse(body);
			var data = document.RootElement.GetProperty("data");
			var ordered = new float[data.GetArrayLength()][];
			foreach (var item in data.EnumerateArray())
			{
				var index = item.GetProperty("index").GetInt32();
				var vector = item.GetProperty("embedding").EnumerateArray().Select(x => x.GetSingle()).ToArray();
				ordered[index] = vector;
			}

			if (ordered.Any(x => x is null))
			{
				throw new AiCallException("Qwen embedding 响应缺少部分 index，无法保证顺序对齐。");
			}

			_logger.LogDebug("Qwen embedding 完成一批：{Count} 条，维度 {Dimension}", batch.Length, ordered[0].Length);
			return ordered.ToList();
		}

		private void EnsureConfigured()
		{
			if (string.IsNullOrWhiteSpace(_options.QwenApiKey))
			{
				throw new AiCallException("未配置 Qwen API Key（环境变量 Ai__QwenApiKey）。");
			}
		}
	}
}
```

**Step 3：`DeepSeekChatService`（Semantic Kernel 的 OpenAI 兼容连接器）**

```csharp
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace KnowVault.Ai
{
	/// <summary>
	/// DeepSeek 生成实现：用 Semantic Kernel 的 OpenAI 兼容连接器指向 DeepSeek 的 /chat/completions。
	/// </summary>
	public class DeepSeekChatService : ILlmChatService, ITransientDependency
	{
		private readonly Kernel _kernel;
		private readonly ILogger<DeepSeekChatService> _logger;

		/// <summary>构造函数。</summary>
		/// <param name="kernel">由 Ai 模块构建的 Semantic Kernel 实例。</param>
		/// <param name="logger">日志。</param>
		public DeepSeekChatService(Kernel kernel, ILogger<DeepSeekChatService> logger)
		{
			_kernel = kernel;
			_logger = logger;
		}

		/// <inheritdoc />
		public async Task<string> CompleteAsync(
			string systemPrompt,
			string userPrompt,
			CancellationToken cancellationToken = default)
		{
			var chat = _kernel.GetRequiredService<IChatCompletionService>();
			var history = new ChatHistory();
			history.AddSystemMessage(systemPrompt);
			history.AddUserMessage(userPrompt);

			var result = await chat.GetChatMessageContentAsync(history, cancellationToken: cancellationToken);
			var answer = result.Content?.Trim();
			if (string.IsNullOrWhiteSpace(answer))
			{
				throw new AiCallException("DeepSeek 返回了空回答。");
			}

			_logger.LogDebug("DeepSeek 生成完成：{Length} 字符", answer.Length);
			return answer;
		}
	}
}
```

**Step 4：模块注册（Kernel 用工厂构建，避免在注册期读不到配置）**

`KnowVaultAiModule.ConfigureServices` 追加：

```csharp
context.Services.AddHttpClient();

// Kernel 用工厂构建：配置在 ConfigureServices 阶段已可由 IOptions 读取（绑定在上一行完成）
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
```

需要的 using：`Microsoft.Extensions.DependencyInjection`、`Microsoft.Extensions.Options`、`Microsoft.SemanticKernel`。

**Step 5：验证（编译 + 一次真实冒烟）**

```powershell
dotnet build KnowVault.slnx
```

冒烟放在 Task 14（需要 Key 与 HTTP），此处只保证编译通过；**不要**在单测里调外部模型（不稳定、要 Key）。

**Step 6：提交** `feat(ai): 接入 Qwen Embedding 与 DeepSeek 生成`

---

## Task 4：向量库抽象与 Qdrant 实现

**Files:**
- Create: `src/KnowVault.Domain/VectorStore/IVectorStore.cs`
- Create: `src/KnowVault.Domain/VectorStore/VectorModels.cs`（`VectorPoint` / `VectorFilter` / `VectorSearchHit`）
- Create: `src/KnowVault.Domain/VectorStore/ChunkPayload.cs`（**payload 字段名的唯一定义处**，Ai 读、Infra 写都用它，避免字符串漂移）
- Create: `src/KnowVault.EntityFrameworkCore/VectorStore/QdrantVectorStore.cs`
- Modify: `src/KnowVault.EntityFrameworkCore/KnowVault.EntityFrameworkCore.csproj`（加 `Qdrant.Client 1.19.0`）
- Modify: `src/KnowVault.EntityFrameworkCore/EntityFrameworkCore/KnowVaultEntityFrameworkCoreModule.cs`（注册 `QdrantClient` 与 `IVectorStore`）
- Test: `tests/KnowVault.Tests/ChunkPayloadTests.cs`

**Step 1：契约（Domain，扁平命名空间）**

```csharp
namespace KnowVault.Domain
{
	/// <summary>向量库访问抽象（实现放 Infrastructure 层，见 TECH_STACK §9.1）。</summary>
	public interface IVectorStore
	{
		/// <summary>幂等创建 collection（已存在且维度一致则什么都不做）。</summary>
		/// <param name="cancellationToken">取消令牌。</param>
		Task EnsureCollectionAsync(CancellationToken cancellationToken = default);

		/// <summary>批量写入/覆盖向量点。</summary>
		/// <param name="points">向量点集合。</param>
		/// <param name="cancellationToken">取消令牌。</param>
		Task UpsertAsync(IReadOnlyList<VectorPoint> points, CancellationToken cancellationToken = default);

		/// <summary>按过滤条件做语义检索。</summary>
		/// <param name="queryVector">查询向量。</param>
		/// <param name="filter">payload 过滤条件。</param>
		/// <param name="topK">返回条数。</param>
		/// <param name="cancellationToken">取消令牌。</param>
		/// <returns>按相似度降序的命中列表。</returns>
		Task<IReadOnlyList<VectorSearchHit>> SearchAsync(
			float[] queryVector,
			VectorFilter filter,
			int topK,
			CancellationToken cancellationToken = default);
	}
}
```

```csharp
namespace KnowVault.Domain
{
	/// <summary>待写入的向量点。</summary>
	public class VectorPoint
	{
		/// <summary>点 ID，取 <c>DocumentChunk.Id</c>。</summary>
		public Guid Id { get; set; }

		/// <summary>向量数据。</summary>
		public float[] Vector { get; set; } = [];

		/// <summary>payload（字段名见 <see cref="ChunkPayload"/>）。</summary>
		public Dictionary<string, object> Payload { get; set; } = [];
	}

	/// <summary>检索过滤条件（P1 只用租户 + 知识库两个维度）。</summary>
	public class VectorFilter
	{
		/// <summary>租户 ID；调用方须把 null 归一化为 <see cref="Guid.Empty"/>。</summary>
		public Guid TenantId { get; set; }

		/// <summary>知识库 ID。</summary>
		public Guid KnowledgeBaseId { get; set; }
	}

	/// <summary>检索命中。</summary>
	public class VectorSearchHit
	{
		/// <summary>点 ID（即 chunk ID）。</summary>
		public Guid Id { get; set; }

		/// <summary>相似度得分。</summary>
		public double Score { get; set; }

		/// <summary>payload。</summary>
		public Dictionary<string, object> Payload { get; set; } = [];
	}
}
```

```csharp
using KnowVault.Domain.Shared;

namespace KnowVault.Domain
{
	/// <summary>
	/// Qdrant payload 的字段名与读写映射（单一 collection 的 payload 契约，见 TECH_STACK §9.1）。
	/// 字段名只在这里定义，避免 Ai 层读、Infra 层写时字符串漂移。
	/// </summary>
	public static class ChunkPayload
	{
		/// <summary>租户 ID（字符串形式的 GUID；null 归一化为 Guid.Empty）。</summary>
		public const string TenantId = "tenantId";

		/// <summary>知识库 ID。</summary>
		public const string KnowledgeBaseId = "knowledgeBaseId";

		/// <summary>文档 ID。</summary>
		public const string DocumentId = "documentId";

		/// <summary>切片序号（0 起）。</summary>
		public const string ChunkIndex = "chunkIndex";

		/// <summary>切片正文（检索命中后直接用于组装 Prompt，不回表）。</summary>
		public const string Content = "content";

		/// <summary>文档标题（冗余存一份，让引用溯源无需回表）。</summary>
		public const string DocumentTitle = "documentTitle";

		/// <summary>标题层级路径，例如「第三章 &gt; 3.2 部署」。</summary>
		public const string HeadingPath = "headingPath";

		/// <summary>来源格式（<see cref="DocumentSourceTypeEnum"/> 的 int 值）。</summary>
		public const string SourceType = "sourceType";

		/// <summary>构造 payload。</summary>
		/// <param name="tenantId">租户 ID（已归一化）。</param>
		/// <param name="knowledgeBaseId">知识库 ID。</param>
		/// <param name="documentId">文档 ID。</param>
		/// <param name="chunkIndex">切片序号。</param>
		/// <param name="content">切片正文。</param>
		/// <param name="documentTitle">文档标题。</param>
		/// <param name="headingPath">标题层级路径。</param>
		/// <param name="sourceType">来源格式。</param>
		/// <returns>可直接写入 Qdrant 的 payload 字典。</returns>
		public static Dictionary<string, object> Create(
			Guid tenantId,
			Guid knowledgeBaseId,
			Guid documentId,
			int chunkIndex,
			string content,
			string documentTitle,
			string headingPath,
			DocumentSourceTypeEnum sourceType) => new()
			{
				[TenantId] = tenantId.ToString(),
				[KnowledgeBaseId] = knowledgeBaseId.ToString(),
				[DocumentId] = documentId.ToString(),
				[ChunkIndex] = chunkIndex,
				[Content] = content,
				[DocumentTitle] = documentTitle,
				[HeadingPath] = headingPath,
				[SourceType] = (int)sourceType
			};

		/// <summary>读取 GUID 字段。</summary>
		/// <param name="payload">payload 字典。</param>
		/// <param name="key">字段名。</param>
		/// <returns>解析出的 GUID；缺失或格式非法时抛 <see cref="InvalidOperationException"/>。</returns>
		public static Guid ReadGuid(IReadOnlyDictionary<string, object> payload, string key)
			=> payload.TryGetValue(key, out var value) && Guid.TryParse(Convert.ToString(value), out var guid)
				? guid
				: throw new InvalidOperationException($"payload 缺少合法的 {key} 字段。");

		/// <summary>读取字符串字段。</summary>
		/// <param name="payload">payload 字典。</param>
		/// <param name="key">字段名。</param>
		/// <returns>字符串值；缺失时返回空串。</returns>
		public static string ReadString(IReadOnlyDictionary<string, object> payload, string key)
			=> payload.TryGetValue(key, out var value) ? Convert.ToString(value) ?? string.Empty : string.Empty;

		/// <summary>读取整数字段。</summary>
		/// <param name="payload">payload 字典。</param>
		/// <param name="key">字段名。</param>
		/// <returns>整数值；缺失或无法转换时为 0。</returns>
		public static int ReadInt(IReadOnlyDictionary<string, object> payload, string key)
			=> payload.TryGetValue(key, out var value) && int.TryParse(Convert.ToString(value), out var number) ? number : 0;
	}
}
```

**Step 2：测试（先写、先看它失败）**

`tests/KnowVault.Tests/ChunkPayloadTests.cs`：

```csharp
using KnowVault.Domain;
using KnowVault.Domain.Shared;
using Xunit;

namespace KnowVault.Tests
{
	/// <summary>payload 契约的读写一致性测试。</summary>
	public class ChunkPayloadTests
	{
		/// <summary>Create 产出的字段必须能被 Read* 原样读回。</summary>
		[Fact]
		public void Create_then_read_round_trips()
		{
			var tenantId = Guid.NewGuid();
			var knowledgeBaseId = Guid.NewGuid();
			var documentId = Guid.NewGuid();

			var payload = ChunkPayload.Create(
				tenantId, knowledgeBaseId, documentId, 3, "正文", "标题", "第一章 > 1.1", DocumentSourceTypeEnum.Pdf);

			Assert.Equal(tenantId, ChunkPayload.ReadGuid(payload, ChunkPayload.TenantId));
			Assert.Equal(knowledgeBaseId, ChunkPayload.ReadGuid(payload, ChunkPayload.KnowledgeBaseId));
			Assert.Equal(documentId, ChunkPayload.ReadGuid(payload, ChunkPayload.DocumentId));
			Assert.Equal(3, ChunkPayload.ReadInt(payload, ChunkPayload.ChunkIndex));
			Assert.Equal("正文", ChunkPayload.ReadString(payload, ChunkPayload.Content));
			Assert.Equal("标题", ChunkPayload.ReadString(payload, ChunkPayload.DocumentTitle));
			Assert.Equal("第一章 > 1.1", ChunkPayload.ReadString(payload, ChunkPayload.HeadingPath));
			Assert.Equal((int)DocumentSourceTypeEnum.Pdf, ChunkPayload.ReadInt(payload, ChunkPayload.SourceType));
		}

		/// <summary>payload 必须包含 TECH_STACK 规定的全部字段。</summary>
		[Fact]
		public void Create_contains_all_required_fields()
		{
			var payload = ChunkPayload.Create(
				Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), 0, "c", "t", "", DocumentSourceTypeEnum.Markdown);

			Assert.Equal(8, payload.Count);
		}

		/// <summary>缺少合法 GUID 字段时必须抛错，而不是静默返回 Guid.Empty。</summary>
		[Fact]
		public void ReadGuid_throws_when_missing()
		{
			var payload = new Dictionary<string, object> { [ChunkPayload.DocumentId] = "不是 GUID" };
			Assert.Throws<InvalidOperationException>(() => ChunkPayload.ReadGuid(payload, ChunkPayload.DocumentId));
		}
	}
}
```

先跑一次确认失败原因符合预期（此时 `ChunkPayload` 还不存在 → 编译失败即"红"）：

```powershell
dotnet test tests\KnowVault.Tests\KnowVault.Tests.csproj
```

期望：编译失败（`ChunkPayload` 未定义）——这就是 TDD 的红灯。

**Step 3：实现 `QdrantVectorStore`（API 已按 Qdrant.Client 1.19 官方 README 核对）**

```csharp
using KnowVault.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using Volo.Abp.DependencyInjection;
using static Qdrant.Client.Grpc.Conditions;

namespace KnowVault.EntityFrameworkCore
{
	/// <summary>
	/// Qdrant 实现的向量库（单例：内部持有 gRPC channel）。
	/// </summary>
	public class QdrantVectorStore : IVectorStore, ISingletonDependency
	{
		private readonly QdrantClient _client;
		private readonly QdrantOptions _options;
		private readonly ILogger<QdrantVectorStore> _logger;
		private readonly SemaphoreSlim _ensureLock = new(1, 1);
		private bool _collectionEnsured;

		/// <summary>构造函数。</summary>
		/// <param name="client">Qdrant 客户端（模块里注册为单例）。</param>
		/// <param name="options">Qdrant 配置。</param>
		/// <param name="logger">日志。</param>
		public QdrantVectorStore(QdrantClient client, IOptions<QdrantOptions> options, ILogger<QdrantVectorStore> logger)
		{
			_client = client;
			_options = options.Value;
			_logger = logger;
		}

		/// <inheritdoc />
		public async Task EnsureCollectionAsync(CancellationToken cancellationToken = default)
		{
			if (_collectionEnsured)
			{
				return;
			}

			await _ensureLock.WaitAsync(cancellationToken);
			try
			{
				if (_collectionEnsured)
				{
					return;
				}

				if (!await _client.CollectionExistsAsync(_options.CollectionName, cancellationToken))
				{
					await _client.CreateCollectionAsync(
						_options.CollectionName,
						new VectorParams { Size = _options.VectorSize, Distance = Distance.Cosine },
						cancellationToken: cancellationToken);
					_logger.LogInformation(
						"已创建 Qdrant collection {Collection}（维度 {Size}，Cosine）",
						_options.CollectionName, _options.VectorSize);
				}

				_collectionEnsured = true;
			}
			finally
			{
				_ensureLock.Release();
			}
		}

		/// <inheritdoc />
		public async Task UpsertAsync(IReadOnlyList<VectorPoint> points, CancellationToken cancellationToken = default)
		{
			if (points.Count == 0)
			{
				return;
			}

			await EnsureCollectionAsync(cancellationToken);

			var structs = points.Select(point => new PointStruct
			{
				Id = new PointId { Uuid = point.Id.ToString() },
				Vectors = point.Vector,
				Payload = { point.Payload.ToDictionary(kv => kv.Key, kv => ToValue(kv.Value)) }
			}).ToList();

			await _client.UpsertAsync(_options.CollectionName, structs, cancellationToken: cancellationToken);
			_logger.LogInformation("已写入 Qdrant {Count} 个向量点", structs.Count);
		}

		/// <inheritdoc />
		public async Task<IReadOnlyList<VectorSearchHit>> SearchAsync(
			float[] queryVector,
			VectorFilter filter,
			int topK,
			CancellationToken cancellationToken = default)
		{
			await EnsureCollectionAsync(cancellationToken);

			// 单租户运行时 filter.TenantId 为 Guid.Empty，与写入侧归一化保持一致
			var qdrantFilter = new Filter
			{
				Must =
				{
					Match(ChunkPayload.TenantId, filter.TenantId.ToString()),
					Match(ChunkPayload.KnowledgeBaseId, filter.KnowledgeBaseId.ToString())
				}
			};

			var points = await _client.SearchAsync(
				_options.CollectionName,
				queryVector,
				filter: qdrantFilter,
				limit: (ulong)topK,
				cancellationToken: cancellationToken);

			return points.Select(point => new VectorSearchHit
			{
				Id = Guid.TryParse(point.Id.Uuid, out var id) ? id : Guid.Empty,
				Score = point.Score,
				Payload = point.Payload.ToDictionary(kv => kv.Key, kv => FromValue(kv.Value))
			}).ToList();
		}

		private static Value ToValue(object value) => value switch
		{
			string text => text,
			int number => number,
			long number => number,
			bool flag => flag,
			double number => number,
			_ => value.ToString() ?? string.Empty
		};

		private static object FromValue(Value value) => value.KindCase switch
		{
			Value.KindOneofCase.StringValue => value.StringValue,
			Value.KindOneofCase.IntegerValue => value.IntegerValue,
			Value.KindOneofCase.DoubleValue => value.DoubleValue,
			Value.KindOneofCase.BoolValue => value.BoolValue,
			_ => string.Empty
		};
	}
}
```

> `ChunkPayload.ReadInt` 已把值 `Convert.ToString` 后再解析，因此 `IntegerValue`（long）与 `StringValue` 都能正确读出。

**Step 4：模块注册**

```csharp
context.Services.AddSingleton(sp =>
{
	var options = sp.GetRequiredService<IOptions<QdrantOptions>>().Value;
	var configuration = new ClientConfiguration();
	if (!options.ApiKey.IsNullOrWhiteSpace())
	{
		configuration.ApiKey = options.ApiKey!;
	}

	var channel = QdrantChannel.ForAddress(new Uri(options.Endpoint), configuration);
	return new QdrantClient(new QdrantGrpcClient(channel));
});
```

（`IVectorStore` 由 `ISingletonDependency` 约定自动注册，无需显式 `AddSingleton`。）

**Step 5：验证**

```powershell
dotnet test tests\KnowVault.Tests\KnowVault.Tests.csproj
```

期望：`Passed!`（3 个 payload 用例 + 1 个冒烟）。若 `CreateCollectionAsync` / `SearchAsync` 的参数名与 1.19 实际签名有出入，**以编译错误为准就地修正**，不要改契约。

**Step 6：提交** `feat(vector): 增加 IVectorStore 抽象与 Qdrant 实现`

---

## Task 5：文档对象存储（MinIO）

**Files:**
- Create: `src/KnowVault.Domain/Storage/IDocumentStorage.cs`
- Create: `src/KnowVault.EntityFrameworkCore/Storage/MinioDocumentStorage.cs`
- Modify: `src/KnowVault.EntityFrameworkCore/KnowVault.EntityFrameworkCore.csproj`（加 `Minio 6.0.5`）
- Modify: `src/KnowVault.EntityFrameworkCore/EntityFrameworkCore/KnowVaultEntityFrameworkCoreModule.cs`（注册 `IMinioClient`）

**Step 1：契约**

```csharp
namespace KnowVault.Domain
{
	/// <summary>原始文档的对象存储抽象（P1 实现为 MinIO）。</summary>
	public interface IDocumentStorage
	{
		/// <summary>幂等创建 bucket。</summary>
		/// <param name="cancellationToken">取消令牌。</param>
		Task EnsureBucketAsync(CancellationToken cancellationToken = default);

		/// <summary>写入对象。</summary>
		/// <param name="objectKey">对象键。</param>
		/// <param name="content">内容流（须支持 Length）。</param>
		/// <param name="contentType">MIME 类型。</param>
		/// <param name="cancellationToken">取消令牌。</param>
		/// <returns>写入的对象键（回显，便于调用方落库）。</returns>
		Task<string> PutAsync(string objectKey, Stream content, string contentType, CancellationToken cancellationToken = default);

		/// <summary>读取对象。</summary>
		/// <param name="objectKey">对象键。</param>
		/// <param name="cancellationToken">取消令牌。</param>
		/// <returns>可读流（调用方负责释放）。</returns>
		Task<Stream> GetAsync(string objectKey, CancellationToken cancellationToken = default);
	}
}
```

**Step 2：实现**

```csharp
using KnowVault.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using Volo.Abp.DependencyInjection;

namespace KnowVault.EntityFrameworkCore
{
	/// <summary>MinIO 实现的对象存储。</summary>
	public class MinioDocumentStorage : IDocumentStorage, ISingletonDependency
	{
		private readonly IMinioClient _client;
		private readonly MinioOptions _options;
		private readonly ILogger<MinioDocumentStorage> _logger;
		private readonly SemaphoreSlim _bucketLock = new(1, 1);
		private bool _bucketEnsured;

		/// <summary>构造函数。</summary>
		/// <param name="client">MinIO 客户端（模块里注册为单例）。</param>
		/// <param name="options">MinIO 配置。</param>
		/// <param name="logger">日志。</param>
		public MinioDocumentStorage(IMinioClient client, IOptions<MinioOptions> options, ILogger<MinioDocumentStorage> logger)
		{
			_client = client;
			_options = options.Value;
			_logger = logger;
		}

		/// <inheritdoc />
		public async Task EnsureBucketAsync(CancellationToken cancellationToken = default)
		{
			if (_bucketEnsured)
			{
				return;
			}

			await _bucketLock.WaitAsync(cancellationToken);
			try
			{
				if (_bucketEnsured)
				{
					return;
				}

				var exists = await _client.BucketExistsAsync(
					new BucketExistsArgs().WithBucket(_options.BucketName), cancellationToken);
				if (!exists)
				{
					await _client.MakeBucketAsync(
						new MakeBucketArgs().WithBucket(_options.BucketName), cancellationToken);
					_logger.LogInformation("已创建 MinIO bucket {Bucket}", _options.BucketName);
				}

				_bucketEnsured = true;
			}
			finally
			{
				_bucketLock.Release();
			}
		}

		/// <inheritdoc />
		public async Task<string> PutAsync(
			string objectKey,
			Stream content,
			string contentType,
			CancellationToken cancellationToken = default)
		{
			await EnsureBucketAsync(cancellationToken);

			await _client.PutObjectAsync(new PutObjectArgs()
				.WithBucket(_options.BucketName)
				.WithObject(objectKey)
				.WithStreamData(content)
				.WithObjectSize(content.Length)
				.WithContentType(contentType), cancellationToken);

			return objectKey;
		}

		/// <inheritdoc />
		public async Task<Stream> GetAsync(string objectKey, CancellationToken cancellationToken = default)
		{
			var memory = new MemoryStream();
			await _client.GetObjectAsync(new GetObjectArgs()
				.WithBucket(_options.BucketName)
				.WithObject(objectKey)
				.WithCallbackStream(stream => stream.CopyTo(memory)), cancellationToken);
			memory.Position = 0;
			return memory;
		}
	}
}
```

**Step 3：模块注册**

```csharp
context.Services.AddSingleton<IMinioClient>(sp =>
{
	var options = sp.GetRequiredService<IOptions<MinioOptions>>().Value;
	if (options.Endpoint.IsNullOrWhiteSpace() || options.AccessKey.IsNullOrWhiteSpace())
	{
		throw new AbpInitializationException("MinIO 未配置：需要 Minio:Endpoint / Minio:AccessKey / Minio:SecretKey（由 AppHost 注入）。");
	}

	return new MinioClient()
		.WithEndpoint(options.Endpoint)
		.WithCredentials(options.AccessKey, options.SecretKey)
		.WithSSL(options.UseSsl)
		.Build();
});
```

**Step 4：验证**

```powershell
dotnet build KnowVault.slnx
```

期望：0 error。若 Minio 6.0.5 的 `PutObjectAsync` 参数名（`cancellationToken` 位置）不一致，以编译错误为准修正。

**Step 5：提交** `feat(storage): 增加 IDocumentStorage 抽象与 MinIO 实现`

---

## Task 6：5 种格式解析器

**Files:**
- Create: `src/KnowVault.Ingest/Parsing/IDocumentParser.cs`
- Create: `src/KnowVault.Ingest/Parsing/ParsedDocument.cs`（含 `ParsedSection`）
- Create: `src/KnowVault.Ingest/Parsing/DocumentParserRegistry.cs`
- Create: `src/KnowVault.Ingest/Parsing/MarkdownDocumentParser.cs`
- Create: `src/KnowVault.Ingest/Parsing/HtmlDocumentParser.cs`
- Create: `src/KnowVault.Ingest/Parsing/PdfDocumentParser.cs`
- Create: `src/KnowVault.Ingest/Parsing/WordDocumentParser.cs`
- Create: `src/KnowVault.Ingest/Parsing/ExcelDocumentParser.cs`
- Modify: `src/KnowVault.Ingest/KnowVault.Ingest.csproj`
- Test: `tests/KnowVault.Tests/DocumentParserTests.cs`

**Step 1：契约**

```csharp
using KnowVault.Domain.Shared;

namespace KnowVault.Ingest
{
	/// <summary>按格式解析文档为「带标题层级的段落集合」。</summary>
	public interface IDocumentParser
	{
		/// <summary>能处理的来源格式。</summary>
		DocumentSourceTypeEnum SourceType { get; }

		/// <summary>能处理的扩展名（小写、含点）。</summary>
		IReadOnlyList<string> Extensions { get; }

		/// <summary>解析文档。</summary>
		/// <param name="content">文档字节流（须可读、可定位）。</param>
		/// <param name="fileName">原始文件名（用于兜底标题）。</param>
		/// <returns>解析结果。</returns>
		ParsedDocument Parse(Stream content, string fileName);
	}
}
```

```csharp
namespace KnowVault.Ingest
{
	/// <summary>解析后的文档（只保留正文结构，丢弃版式）。</summary>
	public class ParsedDocument
	{
		/// <summary>文档标题（取不到时用文件名）。</summary>
		public string Title { get; set; } = string.Empty;

		/// <summary>按阅读顺序排列的段落。</summary>
		public List<ParsedSection> Sections { get; set; } = [];
	}

	/// <summary>带标题层级路径的段落。</summary>
	public class ParsedSection
	{
		/// <summary>标题层级路径，例如「第三章 &gt; 3.2 部署」；无标题时为空串。</summary>
		public string HeadingPath { get; set; } = string.Empty;

		/// <summary>段落正文。</summary>
		public string Text { get; set; } = string.Empty;
	}
}
```

```csharp
using KnowVault.Domain.Shared;

namespace KnowVault.Ingest
{
	/// <summary>按扩展名选择解析器。</summary>
	public class DocumentParserRegistry : Volo.Abp.DependencyInjection.ITransientDependency
	{
		private readonly IReadOnlyList<IDocumentParser> _parsers;

		/// <summary>构造函数。</summary>
		/// <param name="parsers">容器里全部解析器实现。</param>
		public DocumentParserRegistry(IEnumerable<IDocumentParser> parsers)
		{
			_parsers = parsers.ToList();
		}

		/// <summary>按扩展名取解析器。</summary>
		/// <param name="extension">含点的小写扩展名。</param>
		/// <returns>匹配的解析器。</returns>
		/// <exception cref="NotSupportedException">没有解析器能处理该扩展名。</exception>
		public IDocumentParser Resolve(string extension)
			=> _parsers.FirstOrDefault(p => p.Extensions.Contains(extension))
			   ?? throw new NotSupportedException($"没有可处理 {extension} 的文档解析器。");
	}
}
```

**Step 2：`KnowVault.Ingest.csproj` 增加依赖**

```xml
  <ItemGroup>
    <PackageReference Include="PdfPig" Version="0.1.16" />
    <PackageReference Include="DocumentFormat.OpenXml" Version="3.5.1" />
    <PackageReference Include="ClosedXML" Version="0.105.1" />
    <PackageReference Include="Markdig" Version="1.4.0" />
    <PackageReference Include="HtmlAgilityPack" Version="1.13.0" />
  </ItemGroup>
```

（同时删掉 csproj 里那两行注释掉的 `MassTransit.RabbitMQ 9.2.1`，Task 8 会以 8.5.7 正式引入。）

**Step 3：Markdown（Markdig，标题层级靠 AST 树）**

```csharp
using KnowVault.Domain.Shared;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Volo.Abp.DependencyInjection;

namespace KnowVault.Ingest
{
	/// <summary>Markdown 解析器：用 Markdig AST 还原「标题 &gt; 段落」结构。</summary>
	public class MarkdownDocumentParser : IDocumentParser, ITransientDependency
	{
		/// <inheritdoc />
		public DocumentSourceTypeEnum SourceType => DocumentSourceTypeEnum.Markdown;

		/// <inheritdoc />
		public IReadOnlyList<string> Extensions { get; } = [".md", ".markdown"];

		/// <inheritdoc />
		public ParsedDocument Parse(Stream content, string fileName)
		{
			using var reader = new StreamReader(content, leaveOpen: true);
			var markdown = reader.ReadToEnd();
			var document = Markdown.Parse(markdown);
			var result = new ParsedDocument { Title = Path.GetFileNameWithoutExtension(fileName) };

			// 维护 1..6 级标题栈，拼出 "一级 > 二级" 形式的 HeadingPath
			var headings = new string[7];
			foreach (var block in document.Descendants())
			{
				switch (block)
				{
					case HeadingBlock heading when heading.Inline is not null:
						var text = ExtractInlineText(heading.Inline).Trim();
						if (heading.Level is >= 1 and <= 6)
						{
							headings[heading.Level] = text;
							for (var level = heading.Level + 1; level <= 6; level++)
							{
								headings[level] = string.Empty;
							}
						}
						break;

					case ParagraphBlock paragraph when paragraph.Inline is not null:
						var paragraphText = ExtractInlineText(paragraph.Inline).Trim();
						if (paragraphText.Length > 0)
						{
							result.Sections.Add(new ParsedSection
							{
								HeadingPath = BuildHeadingPath(headings),
								Text = paragraphText
							});
						}
						break;

					case ListBlock list:
						var items = list.Descendants<ParagraphBlock>()
							.Select(p => p.Inline is null ? string.Empty : ExtractInlineText(p.Inline).Trim())
							.Where(t => t.Length > 0)
							.ToList();
						if (items.Count > 0)
						{
							result.Sections.Add(new ParsedSection
							{
								HeadingPath = BuildHeadingPath(headings),
								Text = string.Join("\n", items.Select(i => $"- {i}"))
							});
						}
						break;
				}
			}

			return result;
		}

		private static string BuildHeadingPath(string[] headings)
			=> string.Join(" > ", headings.Skip(1).Where(h => !string.IsNullOrWhiteSpace(h)));

		private static string ExtractInlineText(ContainerInline inline)
		{
			var builder = new System.Text.StringBuilder();
			foreach (var literal in inline.Descendants<LiteralInline>())
			{
				builder.Append(literal.Content);
			}

			return builder.ToString();
		}
	}
}
```

> Markdig 1.x 的 `Descendants()` 在 `Markdig.Syntax` 扩展方法里（`using Markdig.Syntax;`）。若 API 有出入，以编译错误为准。

**Step 4：HTML（HtmlAgilityPack，先剔除脚本样式）**

```csharp
using System.Text;
using HtmlAgilityPack;
using KnowVault.Domain.Shared;
using Volo.Abp.DependencyInjection;

namespace KnowVault.Ingest
{
	/// <summary>HTML 解析器：剔除脚本/样式/导航，按 h1-h6 + 段落/列表还原结构。</summary>
	public class HtmlDocumentParser : IDocumentParser, ITransientDependency
	{
		/// <inheritdoc />
		public DocumentSourceTypeEnum SourceType => DocumentSourceTypeEnum.Html;

		/// <inheritdoc />
		public IReadOnlyList<string> Extensions { get; } = [".html", ".htm"];

		/// <inheritdoc />
		public ParsedDocument Parse(Stream content, string fileName)
		{
			var html = new HtmlDocument();
			html.Load(content);

			foreach (var node in html.DocumentNode.SelectNodes("//script|//style|//noscript|//nav|//footer") ?? Enumerable.Empty<HtmlNode>())
			{
				node.Remove();
			}

			var result = new ParsedDocument
			{
				Title = html.DocumentNode.SelectSingleNode("//title")?.InnerText.Trim()
						?? Path.GetFileNameWithoutExtension(fileName)
			};

			var headings = new string[7];
			var body = html.DocumentNode.SelectSingleNode("//body") ?? html.DocumentNode;
			foreach (var node in body.DescendantsAndSelf())
			{
				var name = node.Name.ToLowerInvariant();
				if (name.Length == 2 && name[0] == 'h' && name[1] is >= '1' and <= '6')
				{
					var level = name[1] - '0';
					headings[level] = Normalize(node.InnerText);
					for (var deeper = level + 1; deeper <= 6; deeper++)
					{
						headings[deeper] = string.Empty;
					}

					continue;
				}

				if (name is "p" or "li" or "td" or "blockquote" or "pre")
				{
					var text = Normalize(node.InnerText);
					if (text.Length == 0 || node.ParentNode?.Name.ToLowerInvariant() is "li" or "p")
					{
						continue;
					}

					result.Sections.Add(new ParsedSection
					{
						HeadingPath = string.Join(" > ", headings.Skip(1).Where(h => !string.IsNullOrWhiteSpace(h))),
						Text = text
					});
				}
			}

			return result;
		}

		private static string Normalize(string text)
			=> string.Join(" ", text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
	}
}
```

**Step 5：PDF（PdfPig，按页抽文本）**

```csharp
using KnowVault.Domain.Shared;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using Volo.Abp.DependencyInjection;

namespace KnowVault.Ingest
{
	/// <summary>PDF 解析器：按页抽文本（P1 不还原标题层级，标题路径留空）。</summary>
	public class PdfDocumentParser : IDocumentParser, ITransientDependency
	{
		/// <inheritdoc />
		public DocumentSourceTypeEnum SourceType => DocumentSourceTypeEnum.Pdf;

		/// <inheritdoc />
		public IReadOnlyList<string> Extensions { get; } = [".pdf"];

		/// <inheritdoc />
		public ParsedDocument Parse(Stream content, string fileName)
		{
			using var document = PdfDocument.Open(content);
			var result = new ParsedDocument { Title = Path.GetFileNameWithoutExtension(fileName) };

			foreach (var page in document.GetPages())
			{
				var text = ContentOrderTextExtractor.GetText(page);
				if (string.IsNullOrWhiteSpace(text))
				{
					continue;
				}

				result.Sections.Add(new ParsedSection
				{
					HeadingPath = $"第 {page.Number} 页",
					Text = text.Trim()
				});
			}

			return result;
		}
	}
}
```

**Step 6：Word（OpenXml，用段落样式名识别标题）**

```csharp
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using KnowVault.Domain.Shared;
using Volo.Abp.DependencyInjection;

namespace KnowVault.Ingest
{
	/// <summary>Word(.docx) 解析器：按 Heading1-6 样式还原标题层级，表格按行拼成文本。</summary>
	public class WordDocumentParser : IDocumentParser, ITransientDependency
	{
		/// <inheritdoc />
		public DocumentSourceTypeEnum SourceType => DocumentSourceTypeEnum.Word;

		/// <inheritdoc />
		public IReadOnlyList<string> Extensions { get; } = [".docx"];

		/// <inheritdoc />
		public ParsedDocument Parse(Stream content, string fileName)
		{
			using var document = WordprocessingDocument.Open(content, false);
			var body = document.MainDocumentPart?.Document?.Body
					   ?? throw new InvalidOperationException("docx 缺少正文内容。");

			var result = new ParsedDocument { Title = Path.GetFileNameWithoutExtension(fileName) };
			var headings = new string[7];

			foreach (var element in body.Elements())
			{
				if (element is Paragraph paragraph)
				{
					var text = string.Concat(paragraph.Descendants<Text>().Select(t => t.Text)).Trim();
					if (text.Length == 0)
					{
						continue;
					}

					var level = GetHeadingLevel(paragraph);
					if (level > 0)
					{
						headings[level] = text;
						for (var deeper = level + 1; deeper <= 6; deeper++)
						{
							headings[deeper] = string.Empty;
						}

						continue;
					}

					result.Sections.Add(new ParsedSection
					{
						HeadingPath = string.Join(" > ", headings.Skip(1).Where(h => !string.IsNullOrWhiteSpace(h))),
						Text = text
					});
				}
				else if (element is Table table)
				{
					foreach (var row in table.Elements<TableRow>())
					{
						var cells = row.Elements<TableCell>()
							.Select(c => string.Concat(c.Descendants<Text>().Select(t => t.Text)).Trim())
							.ToList();
						if (cells.Any(c => c.Length > 0))
						{
							result.Sections.Add(new ParsedSection
							{
								HeadingPath = string.Join(" > ", headings.Skip(1).Where(h => !string.IsNullOrWhiteSpace(h))),
								Text = string.Join(" | ", cells)
							});
						}
					}
				}
			}

			return result;
		}

		private static int GetHeadingLevel(Paragraph paragraph)
		{
			var styleId = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
			if (string.IsNullOrWhiteSpace(styleId))
			{
				return 0;
			}

			// 同时兼容英文模板的 Heading1 与中文模板的「标题 1」
			var normalized = styleId.Replace(" ", string.Empty);
			return normalized.StartsWith("Heading", StringComparison.OrdinalIgnoreCase)
				   && int.TryParse(normalized["Heading".Length..], out var level) && level is >= 1 and <= 6
				? level
				: 0;
		}
	}
}
```

**Step 7：Excel（ClosedXML，按工作表分段）**

```csharp
using ClosedXML.Excel;
using KnowVault.Domain.Shared;
using Volo.Abp.DependencyInjection;

namespace KnowVault.Ingest
{
	/// <summary>Excel(.xlsx) 解析器：每个工作表一段，行内单元格用 " | " 连接。</summary>
	public class ExcelDocumentParser : IDocumentParser, ITransientDependency
	{
		/// <inheritdoc />
		public DocumentSourceTypeEnum SourceType => DocumentSourceTypeEnum.Excel;

		/// <inheritdoc />
		public IReadOnlyList<string> Extensions { get; } = [".xlsx"];

		/// <inheritdoc />
		public ParsedDocument Parse(Stream content, string fileName)
		{
			using var workbook = new XLWorkbook(content);
			var result = new ParsedDocument { Title = Path.GetFileNameWithoutExtension(fileName) };

			foreach (var worksheet in workbook.Worksheets)
			{
				var rows = worksheet.RowsUsed()
					.Select(row => string.Join(" | ", row.CellsUsed().Select(cell => cell.GetFormattedString().Trim())))
					.Where(line => line.Replace("|", string.Empty).Trim().Length > 0)
					.ToList();

				if (rows.Count == 0)
				{
					continue;
				}

				result.Sections.Add(new ParsedSection
				{
					HeadingPath = $"工作表 {worksheet.Name}",
					Text = string.Join("\n", rows)
				});
			}

			return result;
		}
	}
}
```

**Step 8：测试（Markdown / HTML 用内联字符串；Word / Excel 用代码现场生成字节，避免二进制夹具）**

```csharp
using System.Text;
using ClosedXML.Excel;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using KnowVault.Domain.Shared;
using KnowVault.Ingest;
using Xunit;

namespace KnowVault.Tests
{
	/// <summary>文档解析器的纯函数测试（不依赖网络与数据库）。</summary>
	public class DocumentParserTests
	{
		/// <summary>Markdown 的标题层级要拼成 HeadingPath，段落按顺序产出。</summary>
		[Fact]
		public void Markdown_parser_builds_heading_path()
		{
			const string markdown = "# 第一章\n\n第一段内容。\n\n## 1.1 小节\n\n第二段内容。\n";
			var parser = new MarkdownDocumentParser();

			var parsed = parser.Parse(new MemoryStream(Encoding.UTF8.GetBytes(markdown)), "架构.md");

			Assert.Equal(2, parsed.Sections.Count);
			Assert.Equal("第一章", parsed.Sections[0].HeadingPath);
			Assert.Equal("第一段内容。", parsed.Sections[0].Text);
			Assert.Equal("第一章 > 1.1 小节", parsed.Sections[1].HeadingPath);
		}

		/// <summary>HTML 要剔除脚本内容，只留正文文本。</summary>
		[Fact]
		public void Html_parser_skips_script_and_style()
		{
			const string html = """
				<html><head><title>测试页</title><style>body{color:red}</style></head>
				<body><h1>标题一</h1><p>正文一段。</p><script>var x=1;</script><ul><li>条目一</li></ul></body></html>
				""";
			var parser = new HtmlDocumentParser();

			var parsed = parser.Parse(new MemoryStream(Encoding.UTF8.GetBytes(html)), "page.html");

			Assert.Equal("测试页", parsed.Title);
			Assert.Contains(parsed.Sections, s => s.Text.Contains("正文一段。"));
			Assert.Contains(parsed.Sections, s => s.Text.Contains("条目一"));
			Assert.DoesNotContain(parsed.Sections, s => s.Text.Contains("var x=1"));
			Assert.DoesNotContain(parsed.Sections, s => s.Text.Contains("color:red"));
		}

		/// <summary>Word：Heading1 样式要进 HeadingPath，正文段落要产出。</summary>
		[Fact]
		public void Word_parser_reads_heading_style()
		{
			using var buffer = new MemoryStream();
			using (var document = WordprocessingDocument.Create(buffer, WordprocessingDocumentType.Document))
			{
				var main = document.AddMainDocumentPart();
				main.Document = new Document(new Body(
					new Paragraph(new ParagraphProperties(new ParagraphStyleId { Val = "Heading1" }), new Run(new Text("第一章"))),
					new Paragraph(new Run(new Text("正文内容")))));
				main.Document.Save();
			}

			buffer.Position = 0;
			var parsed = new WordDocumentParser().Parse(buffer, "设计.docx");

			Assert.Single(parsed.Sections);
			Assert.Equal("第一章", parsed.Sections[0].HeadingPath);
			Assert.Equal("正文内容", parsed.Sections[0].Text);
		}

		/// <summary>Excel：每个工作表产出一段，行内用 " | " 连接。</summary>
		[Fact]
		public void Excel_parser_joins_cells_with_pipe()
		{
			using var buffer = new MemoryStream();
			using (var workbook = new XLWorkbook())
			{
				var sheet = workbook.AddWorksheet("人员");
				sheet.Cell("A1").Value = "姓名";
				sheet.Cell("B1").Value = "部门";
				sheet.Cell("A2").Value = "张三";
				sheet.Cell("B2").Value = "研发";
				workbook.SaveAs(buffer);
			}

			buffer.Position = 0;
			var parsed = new ExcelDocumentParser().Parse(buffer, "名单.xlsx");

			Assert.Single(parsed.Sections);
			Assert.Equal("工作表 人员", parsed.Sections[0].HeadingPath);
			Assert.Contains("姓名 | 部门", parsed.Sections[0].Text);
			Assert.Contains("张三 | 研发", parsed.Sections[0].Text);
		}

		/// <summary>未知扩展名必须抛错，而不是静默返回空文档。</summary>
		[Fact]
		public void Registry_throws_for_unknown_extension()
		{
			var registry = new DocumentParserRegistry([new MarkdownDocumentParser()]);
			Assert.Throws<NotSupportedException>(() => registry.Resolve(".txt"));
		}
	}
}
```

先跑一次确认红灯（`MarkdownDocumentParser` 等尚不存在 → 编译失败），再实现后转绿：

```powershell
dotnet test tests\KnowVault.Tests\KnowVault.Tests.csproj
```

期望（实现后）：`Passed!`，其中 `Excel_parser_joins_cells_with_pipe` 若因区域格式导致 `GetFormattedString()` 返回带千分位等差异，断言用 `Contains` 已足够宽松。

**Step 9：提交** `feat(ingest): 增加 PDF/Word/Excel/Markdown/HTML 解析器`

---

## Task 7：切片器（核心算法，TDD 重点）

**Files:**
- Create: `src/KnowVault.Ingest/Chunking/IDocumentChunker.cs`
- Create: `src/KnowVault.Ingest/Chunking/TextChunk.cs`
- Create: `src/KnowVault.Ingest/Chunking/DocumentChunker.cs`
- Test: `tests/KnowVault.Tests/DocumentChunkerTests.cs`

**设计（为什么这么切）**：按「段落/标题」优先 —— 先把每个 section 的正文按空行拆成块（block），贪心累积到接近 `ChunkSizeChars` 就落一个 chunk；新 chunk 从上一 chunk 的**尾部 overlap 个字符**起步，保证跨界语义不断。单个 block 本身超长时按句子边界硬切（中文按 `。！？；`，英文按 `.!?;`），**此时不叠加 overlap**（避免切片被撑爆），这一点作为 P1 的已知取舍写进注释。

**Step 1：契约与模型**

```csharp
namespace KnowVault.Ingest
{
	/// <summary>把解析结果切成可向量化的切片。</summary>
	public interface IDocumentChunker
	{
		/// <summary>切片。</summary>
		/// <param name="document">解析后的文档。</param>
		/// <returns>按顺序排列的切片（Index 从 0 起连续）。</returns>
		IReadOnlyList<TextChunk> Chunk(ParsedDocument document);
	}
}
```

```csharp
namespace KnowVault.Ingest
{
	/// <summary>一个待向量化的切片。</summary>
	public class TextChunk
	{
		/// <summary>切片序号（0 起）。</summary>
		public int Index { get; set; }

		/// <summary>切片正文。</summary>
		public string Content { get; set; } = string.Empty;

		/// <summary>标题层级路径（用于引用溯源）。</summary>
		public string HeadingPath { get; set; } = string.Empty;
	}
}
```

**Step 2：先写测试（红灯）**

```csharp
using KnowVault.Ingest;
using Microsoft.Extensions.Options;
using Xunit;

namespace KnowVault.Tests
{
	/// <summary>切片器行为测试（纯函数、无 IO）。</summary>
	public class DocumentChunkerTests
	{
		private static DocumentChunker CreateChunker(int size = 100, int overlap = 20)
			=> new(Options.Create(new IngestOptions { ChunkSizeChars = size, ChunkOverlapChars = overlap }));

		private static ParsedDocument Document(params (string Heading, string Text)[] sections)
		{
			var document = new ParsedDocument { Title = "测试文档" };
			foreach (var (heading, text) in sections)
			{
				document.Sections.Add(new ParsedSection { HeadingPath = heading, Text = text });
			}

			return document;
		}

		/// <summary>短文档只产出一个切片，正文不丢字。</summary>
		[Fact]
		public void Short_document_produces_single_chunk()
		{
			var chunks = CreateChunker().Chunk(Document(("", "很短的一段话。")));

			Assert.Single(chunks);
			Assert.Equal(0, chunks[0].Index);
			Assert.Contains("很短的一段话。", chunks[0].Content);
		}

		/// <summary>多个短段落在容量允许时应合并进同一个切片。</summary>
		[Fact]
		public void Small_blocks_are_merged()
		{
			var text = string.Join("\n\n", Enumerable.Repeat("十字符左右的一段。", 3));
			var chunks = CreateChunker(size: 200).Chunk(Document(("", text)));

			Assert.Single(chunks);
		}

		/// <summary>超出容量的段落必须切分为多个切片，且每个切片不超长。</summary>
		[Fact]
		public void Every_chunk_respects_size_limit()
		{
			var text = string.Join("\n\n", Enumerable.Repeat(new string('测', 80), 6));
			var chunks = CreateChunker(size: 100, overlap: 20).Chunk(Document(("", text)));

			Assert.True(chunks.Count > 1);
			Assert.All(chunks, c => Assert.True(c.Content.Length <= 100, $"切片长度 {c.Content.Length} 超过 100"));
		}

		/// <summary>相邻切片之间要带重叠内容（避免跨界语义断裂）。</summary>
		[Fact]
		public void Consecutive_chunks_overlap()
		{
			var first = new string('甲', 90);
			var second = new string('乙', 90);
			var chunks = CreateChunker(size: 100, overlap: 20).Chunk(Document(("", $"{first}\n\n{second}")));

			Assert.Equal(2, chunks.Count);
			Assert.Contains(new string('甲', 20), chunks[1].Content);
		}

		/// <summary>超长单段落要按句子边界硬切，不产生超长切片。</summary>
		[Fact]
		public void Oversized_block_is_hard_split()
		{
			var sentence = new string('字', 45) + "。";
			var text = string.Concat(Enumerable.Repeat(sentence, 10));
			var chunks = CreateChunker(size: 100, overlap: 20).Chunk(Document(("", text)));

			Assert.True(chunks.Count >= 5);
			Assert.All(chunks, c => Assert.True(c.Content.Length <= 100));
		}

		/// <summary>切片的 HeadingPath 必须继承所属段落的标题路径。</summary>
		[Fact]
		public void Chunks_inherit_heading_path()
		{
			var chunks = CreateChunker(size: 60)
				.Chunk(Document(("第一章", new string('甲', 200)), ("第二章", new string('乙', 200))));

			Assert.Contains(chunks, c => c.HeadingPath == "第一章");
			Assert.Contains(chunks, c => c.HeadingPath == "第二章");
		}

		/// <summary>空文档产出 0 个切片，不能凭空造内容。</summary>
		[Fact]
		public void Empty_document_produces_no_chunk()
		{
			Assert.Empty(CreateChunker().Chunk(Document(("", "   "), ("标题", ""))));
		}
	}
}
```

**Step 3：跑测试确认红灯**

```powershell
dotnet test tests\KnowVault.Tests\KnowVault.Tests.csproj --filter DocumentChunkerTests
```

期望：编译失败（`DocumentChunker` 未定义）。

**Step 4：实现（完整算法）**

```csharp
using System.Text;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace KnowVault.Ingest
{
	/// <summary>
	/// 默认切片器：段落优先 + 固定长度上限 + 尾部重叠。
	/// 长度单位是「字符」，用于近似 token（中文没有可靠 tokenizer，见 TECH_STACK §9.1）。
	/// </summary>
	public class DocumentChunker : IDocumentChunker, ITransientDependency
	{
		private static readonly char[] SentenceEndings = ['。', '！', '？', '；', '!', '?', ';', '\n'];

		private readonly IngestOptions _options;

		/// <summary>构造函数。</summary>
		/// <param name="options">摄入参数（切片长度与重叠）。</param>
		public DocumentChunker(IOptions<IngestOptions> options)
		{
			_options = options.Value;
		}

		/// <inheritdoc />
		public IReadOnlyList<TextChunk> Chunk(ParsedDocument document)
		{
			var size = Math.Max(100, _options.ChunkSizeChars);
			// 重叠不超过容量的一半，否则切片会互相吞没
			var overlap = Math.Clamp(_options.ChunkOverlapChars, 0, size / 2);
			var chunks = new List<TextChunk>();

			foreach (var section in document.Sections)
			{
				var buffer = new StringBuilder();

				foreach (var block in SplitIntoBlocks(section.Text))
				{
					foreach (var piece in SplitOversizedBlock(block, size))
					{
						if (buffer.Length > 0 && buffer.Length + piece.Length + 1 > size)
						{
							chunks.Add(CreateChunk(chunks.Count, buffer.ToString(), section.HeadingPath));

							// 只有当「重叠段 + 本块」仍放得下时才带重叠，否则宁可断开也不超长
							var tail = TakeTail(buffer.ToString(), overlap);
							buffer.Clear();
							if (tail.Length > 0 && tail.Length + piece.Length + 1 <= size)
							{
								buffer.Append(tail).Append('\n');
							}
						}

						buffer.Append(piece).Append('\n');
					}
				}

				if (buffer.ToString().Trim().Length > 0)
				{
					chunks.Add(CreateChunk(chunks.Count, buffer.ToString(), section.HeadingPath));
				}
			}

			return chunks;
		}

		private static TextChunk CreateChunk(int index, string content, string headingPath) => new()
		{
			Index = index,
			Content = content.Trim(),
			HeadingPath = headingPath
		};

		/// <summary>按空行把段落拆成块，并统一换行符。</summary>
		/// <param name="text">段落正文。</param>
		/// <returns>非空的文本块。</returns>
		private static IEnumerable<string> SplitIntoBlocks(string text)
			=> text.Replace("\r\n", "\n")
				.Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

		/// <summary>把超长块按句子边界切成不超过 <paramref name="size"/> 的片段。</summary>
		/// <param name="block">文本块。</param>
		/// <param name="size">单片最大字符数。</param>
		/// <returns>片段序列。</returns>
		private static IEnumerable<string> SplitOversizedBlock(string block, int size)
		{
			var remaining = block.Trim();
			while (remaining.Length > size)
			{
				var cut = FindCutIndex(remaining, size);
				yield return remaining[..cut].Trim();
				remaining = remaining[cut..].TrimStart();
			}

			if (remaining.Length > 0)
			{
				yield return remaining;
			}
		}

		/// <summary>在容量内寻找最靠后的句子结束符；找不到就硬切。</summary>
		/// <param name="text">文本。</param>
		/// <param name="size">容量。</param>
		/// <returns>切割位置（不切分结束符本身）。</returns>
		private static int FindCutIndex(string text, int size)
		{
			for (var index = Math.Min(size, text.Length) - 1; index > size / 4; index--)
			{
				if (SentenceEndings.Contains(text[index]))
				{
					return index + 1;
				}
			}

			return Math.Min(size, text.Length);
		}

		/// <summary>取尾部若干字符作为重叠内容。</summary>
		/// <param name="text">文本。</param>
		/// <param name="overlap">重叠长度。</param>
		/// <returns>尾部片段；不足时返回空串。</returns>
		private static string TakeTail(string text, int overlap)
			=> overlap <= 0 || text.Length <= overlap ? string.Empty : text[^overlap..].Trim();
	}
}
```

**Step 5：跑测试确认绿灯**

```powershell
dotnet test tests\KnowVault.Tests\KnowVault.Tests.csproj --filter DocumentChunkerTests
```

期望：7 个用例全 `Passed`。若 `Consecutive_chunks_overlap` 失败，先检查 `TakeTail` 是否会因 `Trim()` 把重叠内容裁掉（`甲` 不会被裁，`乙` 场景才是关键）。

**Step 6：提交** `feat(ingest): 增加段落优先+固定长度+尾部重叠的切片器`

---

## Task 8：Ingest 链路与 MassTransit 消费

**Files:**
- Modify: `src/KnowVault.Ingest/KnowVault.Ingest.csproj`（加 `MassTransit` / `MassTransit.RabbitMQ` **8.5.7**）
- Create: `src/KnowVault.Domain/Messaging/IDocumentIngestPublisher.cs`（**接口在 Domain：应用层不依赖 MassTransit**）
- Create: `src/KnowVault.Ingest/Messaging/DocumentIngestRequested.cs`
- Create: `src/KnowVault.Ingest/Messaging/MassTransitDocumentIngestPublisher.cs`
- Create: `src/KnowVault.Ingest/Messaging/DocumentIngestConsumer.cs`
- Create: `src/KnowVault.Ingest/Processing/QdrantCollectionInitializer.cs`
- Modify: `src/KnowVault.Ingest/Processing/IDocumentProcessor.cs`（改成按文档处理）
- Modify: `src/KnowVault.Ingest/Processing/DocumentProcessor.cs`（真实现）
- Modify: `src/KnowVault.Ingest/KnowVaultIngestModule.cs`（MassTransit + 后台初始化）

**Step 1：发布接口（Domain）**

```csharp
namespace KnowVault.Domain
{
	/// <summary>发布「文档待摄入」消息的抽象（实现放 Ingest 层，避免应用层耦合具体 MQ 框架）。</summary>
	public interface IDocumentIngestPublisher
	{
		/// <summary>发布摄入请求。</summary>
		/// <param name="documentId">文档 ID。</param>
		/// <param name="tenantId">租户 ID（可为 null，消费侧会归一化）。</param>
		/// <param name="cancellationToken">取消令牌。</param>
		Task PublishAsync(Guid documentId, Guid? tenantId, CancellationToken cancellationToken = default);
	}
}
```

**Step 2：消息契约与发布者**

```csharp
namespace KnowVault.Ingest
{
	/// <summary>「文档待摄入」消息。用可写属性（而非 record 位置参数），保证跨进程序列化稳定。</summary>
	public class DocumentIngestRequested
	{
		/// <summary>文档 ID。</summary>
		public Guid DocumentId { get; init; }

		/// <summary>租户 ID。</summary>
		public Guid? TenantId { get; init; }
	}
}
```

```csharp
using KnowVault.Domain;
using MassTransit;
using Volo.Abp.DependencyInjection;

namespace KnowVault.Ingest
{
	/// <summary>用 MassTransit 发布摄入请求。</summary>
	public class MassTransitDocumentIngestPublisher : IDocumentIngestPublisher, ITransientDependency
	{
		private readonly IPublishEndpoint _publishEndpoint;

		/// <summary>构造函数。</summary>
		/// <param name="publishEndpoint">MassTransit 发布端点。</param>
		public MassTransitDocumentIngestPublisher(IPublishEndpoint publishEndpoint)
		{
			_publishEndpoint = publishEndpoint;
		}

		/// <inheritdoc />
		public Task PublishAsync(Guid documentId, Guid? tenantId, CancellationToken cancellationToken = default)
			=> _publishEndpoint.Publish(
				new DocumentIngestRequested { DocumentId = documentId, TenantId = tenantId },
				cancellationToken);
	}
}
```

**Step 3：consumer（必须自己开 UoW 与切租户）**

这是 ABP + MassTransit 最容易踩的地方：consumer 不在 HTTP 请求管线里，**没有 ABP 的工作单元、也没有租户上下文**，必须显式开。

```csharp
using KnowVault.Domain;
using MassTransit;
using Microsoft.Extensions.Logging;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace KnowVault.Ingest
{
	/// <summary>
	/// 文档摄入消费者。
	/// 进程内运行（P1 决策 1），但保持宿主无关：自带 UoW 与租户切换，可直接平移到独立 Worker。
	/// 处理失败只记录到 Document（不重投），P2 再加重试与死信策略。
	/// </summary>
	public class DocumentIngestConsumer : IConsumer<DocumentIngestRequested>
	{
		private readonly IDocumentProcessor _processor;
		private readonly IUnitOfWorkManager _unitOfWorkManager;
		private readonly ICurrentTenant _currentTenant;
		private readonly ILogger<DocumentIngestConsumer> _logger;

		/// <summary>构造函数。</summary>
		/// <param name="processor">文档处理管线。</param>
		/// <param name="unitOfWorkManager">工作单元管理器。</param>
		/// <param name="currentTenant">当前租户。</param>
		/// <param name="logger">日志。</param>
		public DocumentIngestConsumer(
			IDocumentProcessor processor,
			IUnitOfWorkManager unitOfWorkManager,
			ICurrentTenant currentTenant,
			ILogger<DocumentIngestConsumer> logger)
		{
			_processor = processor;
			_unitOfWorkManager = unitOfWorkManager;
			_currentTenant = currentTenant;
			_logger = logger;
		}

		/// <inheritdoc />
		public async Task Consume(ConsumeContext<DocumentIngestRequested> context)
		{
			var message = context.Message;
			_logger.LogInformation("开始摄入文档 {DocumentId}（租户 {TenantId}）", message.DocumentId, message.TenantId);

			using (_currentTenant.Change(message.TenantId))
			using (var unitOfWork = _unitOfWorkManager.Begin(requiresNew: true))
			{
				await _processor.ProcessAsync(message.DocumentId, context.CancellationToken);
				await unitOfWork.CompleteAsync();
			}
		}
	}
}
```

**Step 4：处理管线**

```csharp
namespace KnowVault.Ingest
{
	/// <summary>文档处理入口：解析 → 切片 → 向量化 → 写入 Qdrant → 推进状态。</summary>
	public interface IDocumentProcessor
	{
		/// <summary>处理单个文档（失败时把错误写进文档状态，不抛异常）。</summary>
		/// <param name="documentId">文档 ID。</param>
		/// <param name="cancellationToken">取消令牌。</param>
		Task ProcessAsync(Guid documentId, CancellationToken cancellationToken = default);
	}
}
```

```csharp
using KnowVault.Domain;
using KnowVault.Domain.Shared;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

namespace KnowVault.Ingest
{
	/// <summary>
	/// 文档处理管线。失败一律记录到 <see cref="Document.Status"/>=Failed 并结束（P1 不做自动重试）。
	/// </summary>
	public class DocumentProcessor : IDocumentProcessor, ITransientDependency
	{
		private readonly IRepository<Document, Guid> _documentRepository;
		private readonly IRepository<DocumentChunk, Guid> _chunkRepository;
		private readonly IDocumentStorage _storage;
		private readonly DocumentParserRegistry _parserRegistry;
		private readonly IDocumentChunker _chunker;
		private readonly IEmbeddingService _embeddingService;
		private readonly IVectorStore _vectorStore;
		private readonly IGuidGenerator _guidGenerator;
		private readonly ILogger<DocumentProcessor> _logger;

		/// <summary>构造函数。</summary>
		/// <param name="documentRepository">文档仓储。</param>
		/// <param name="chunkRepository">切片仓储。</param>
		/// <param name="storage">对象存储。</param>
		/// <param name="parserRegistry">解析器注册表。</param>
		/// <param name="chunker">切片器。</param>
		/// <param name="embeddingService">向量化服务。</param>
		/// <param name="vectorStore">向量库。</param>
		/// <param name="guidGenerator">GUID 生成器（ABP 约定）。</param>
		/// <param name="logger">日志。</param>
		public DocumentProcessor(
			IRepository<Document, Guid> documentRepository,
			IRepository<DocumentChunk, Guid> chunkRepository,
			IDocumentStorage storage,
			DocumentParserRegistry parserRegistry,
			IDocumentChunker chunker,
			IEmbeddingService embeddingService,
			IVectorStore vectorStore,
			IGuidGenerator guidGenerator,
			ILogger<DocumentProcessor> logger)
		{
			_documentRepository = documentRepository;
			_chunkRepository = chunkRepository;
			_storage = storage;
			_parserRegistry = parserRegistry;
			_chunker = chunker;
			_embeddingService = embeddingService;
			_vectorStore = vectorStore;
			_guidGenerator = guidGenerator;
			_logger = logger;
		}

		/// <inheritdoc />
		public async Task ProcessAsync(Guid documentId, CancellationToken cancellationToken = default)
		{
			var document = await _documentRepository.FindAsync(documentId, cancellationToken: cancellationToken);
			if (document is null)
			{
				_logger.LogWarning("文档 {DocumentId} 不存在，跳过处理", documentId);
				return;
			}

			try
			{
				await ProcessInternalAsync(document, cancellationToken);
			}
			catch (Exception exception)
			{
				// P1 策略：把失败固化到文档上，事务照常提交，消息常 ACK（不重投）
				_logger.LogError(exception, "文档 {DocumentId} 摄入失败", documentId);
				document.MarkFailed(exception.Message);
				await _documentRepository.UpdateAsync(document, autoSave: true, cancellationToken: cancellationToken);
			}
		}

		private async Task ProcessInternalAsync(Document document, CancellationToken cancellationToken)
		{
			if (string.IsNullOrWhiteSpace(document.ObjectStorageKey))
			{
				throw new InvalidOperationException("文档缺少对象存储键，无法解析。");
			}

			document.MarkProcessing();
			await _documentRepository.UpdateAsync(document, autoSave: true, cancellationToken: cancellationToken);

			var extension = Path.GetExtension(document.ObjectStorageKey).ToLowerInvariant();
			var parser = _parserRegistry.Resolve(extension);

			ParsedDocument parsed;
			await using (var stream = await _storage.GetAsync(document.ObjectStorageKey!, cancellationToken))
			{
				parsed = parser.Parse(stream, document.ObjectStorageKey!);
			}

			var chunks = _chunker.Chunk(parsed);
			if (chunks.Count == 0)
			{
				throw new InvalidOperationException("未从文档中解析出任何文本内容。");
			}

			var vectors = await _embeddingService.EmbedAsync(
				chunks.Select(c => c.Content).ToList(), cancellationToken);

			var tenantId = document.TenantId ?? Guid.Empty;
			var points = new List<VectorPoint>(chunks.Count);
			var entities = new List<DocumentChunk>(chunks.Count);

			for (var index = 0; index < chunks.Count; index++)
			{
				var chunk = chunks[index];
				var chunkId = _guidGenerator.Create();

				points.Add(new VectorPoint
				{
					Id = chunkId,
					Vector = vectors[index],
					Payload = ChunkPayload.Create(
						tenantId,
						document.KnowledgeBaseId,
						document.Id,
						chunk.Index,
						chunk.Content,
						document.Title,
						chunk.HeadingPath,
						document.SourceType)
				});

				entities.Add(new DocumentChunk(chunkId, document.Id, chunk.Index, chunk.Content, document.TenantId));
			}

			await _vectorStore.UpsertAsync(points, cancellationToken);
			await _chunkRepository.InsertManyAsync(entities, autoSave: true, cancellationToken: cancellationToken);

			document.MarkReady();
			await _documentRepository.UpdateAsync(document, autoSave: true, cancellationToken: cancellationToken);
			_logger.LogInformation("文档 {DocumentId} 摄入完成：{Count} 个切片", document.Id, chunks.Count);
		}
	}
}
```

> 注意：`DocumentChunk.Content` 与 Qdrant payload 各存一份（表里那份便于调试/直查，payload 那份避免检索回表），这是 P0 已定的冗余策略。

**Step 5：Qdrant collection 后台初始化（放在 Ingest 模块 → DbMigrator 不受影响）**

```csharp
using KnowVault.Domain;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KnowVault.Ingest
{
	/// <summary>
	/// 启动时幂等确保 Qdrant collection 存在。
	/// 失败只记日志不阻断启动（Qdrant 未就绪时，检索/写入会各自重试）。
	/// </summary>
	public class QdrantCollectionInitializer : IHostedService
	{
		private readonly IVectorStore _vectorStore;
		private readonly ILogger<QdrantCollectionInitializer> _logger;

		/// <summary>构造函数。</summary>
		/// <param name="vectorStore">向量库。</param>
		/// <param name="logger">日志。</param>
		public QdrantCollectionInitializer(IVectorStore vectorStore, ILogger<QdrantCollectionInitializer> logger)
		{
			_vectorStore = vectorStore;
			_logger = logger;
		}

		/// <inheritdoc />
		public async Task StartAsync(CancellationToken cancellationToken)
		{
			try
			{
				await _vectorStore.EnsureCollectionAsync(cancellationToken);
			}
			catch (Exception exception)
			{
				_logger.LogError(exception, "初始化 Qdrant collection 失败，应用继续启动");
			}
		}

		/// <inheritdoc />
		public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
	}
}
```

**Step 6：模块注册（含 MassTransit）**

```csharp
using KnowVault.Domain;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.Modularity;

namespace KnowVault.Ingest
{
	/// <summary>
	/// 文档摄入模块：解析 / 切片 / 向量化后写入 Qdrant。
	/// P1 采用进程内 consumer（仍走 RabbitMQ），保持宿主无关以便 P3 拆独立 Worker。
	/// </summary>
	[DependsOn(
		typeof(KnowVaultDomainModule),
		typeof(KnowVaultEntityFrameworkCoreModule))]
	public class KnowVaultIngestModule : AbpModule
	{
		/// <inheritdoc />
		public override void ConfigureServices(ServiceConfigurationContext context)
		{
			var configuration = context.Services.GetConfiguration();
			Configure<IngestOptions>(configuration.GetSection("Ingest"));

			context.Services.AddMassTransit(bus =>
			{
				bus.AddConsumer<DocumentIngestConsumer>();

				bus.UsingRabbitMq((busContext, rabbit) =>
				{
					var connectionString = configuration.GetConnectionString("rabbitmq");
					if (connectionString.IsNullOrWhiteSpace())
					{
						throw new AbpInitializationException(
							"未配置 RabbitMQ：需要 ConnectionStrings:rabbitmq（由 Aspire 的 WithReference 注入）。");
					}

					rabbit.Host(new Uri(connectionString));
					rabbit.ConfigureEndpoints(busContext);
				});
			});

			// 只在 Ingest 模块做 Qdrant 初始化：DbMigrator 不加载本模块，避免迁移时去连向量库
			context.Services.AddHostedService<QdrantCollectionInitializer>();
		}
	}
}
```

**Step 7：验证**

```powershell
dotnet build KnowVault.slnx
```

期望：0 error。若 MassTransit 的 `AddMassTransit` 与 ABP Autofac 出现解析期告警，先确认 `bus.AddConsumer<T>()` 的 consumer 能被根容器解析（ABP 的 `ITransientDependency` 已自动注册）。

**Step 8：提交** `feat(ingest): 接入 MassTransit 消费与解析/切片/向量化处理管线`

---

## Task 9：上传 / 知识库 API 与默认知识库种子

**Files:**
- Create: `src/KnowVault.Application.Contracts/Models/Documents/UploadDocumentInput.cs`
- Create: `src/KnowVault.Application.Contracts/Models/KnowledgeBases/KnowledgeBaseDto.cs`
- Create: `src/KnowVault.Application.Contracts/Interfaces/IKnowledgeBaseAppService.cs`
- Create: `src/KnowVault.Application/KnowledgeBases/KnowledgeBaseAppService.cs`
- Create: `src/KnowVault.Domain/Seeds/KnowledgeBaseDataSeedContributor.cs`
- Modify: `src/KnowVault.Domain/KnowVaultConsts.cs`（加默认知识库 ID）
- Modify: `src/KnowVault.Application/Documents/DocumentAppService.cs`（上传 + 发布消息）
- Modify: `src/KnowVault.Application.Contracts/Interfaces/IDocumentAppService.cs`
- Modify: `src/KnowVault.Application/KnowledgeBases/…`、`QaAppService`（加 `[Authorize]`）

> **前置核实（已核实，记录在此供执行者确认）**：`IRemoteStreamContent` 定义在 **`Volo.Abp.Core`** 的 `Volo.Abp.Content` 命名空间（已用二进制检索确认），**无需新增包引用**即可在 `Application.Contracts` 使用。

**Step 1：常量**

`KnowVaultConsts` 追加：

```csharp
	/// <summary>P1 内置默认知识库的固定 ID（MVP 不做知识库管理界面，前端默认选它）。</summary>
	public static readonly Guid DefaultKnowledgeBaseId = Guid.Parse("00000000-0000-0000-0000-000000000001");
```

**Step 2：输入输出契约**

```csharp
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Content;

namespace KnowVault.Application.Contracts
{
	/// <summary>上传文档入参（multipart/form-data，由 ABP 动态 API 绑定）。</summary>
	public class UploadDocumentInput
	{
		/// <summary>目标知识库。</summary>
		[Required]
		public Guid KnowledgeBaseId { get; set; }

		/// <summary>可选标题；为空则取文件名（不含扩展名）。</summary>
		[StringLength(256)]
		public string? Title { get; set; }

		/// <summary>上传的文件流。</summary>
		[Required]
		public IRemoteStreamContent File { get; set; } = default!;
	}
}
```

```csharp
namespace KnowVault.Application.Contracts
{
	/// <summary>知识库 DTO。</summary>
	public class KnowledgeBaseDto
	{
		/// <summary>知识库 ID。</summary>
		public Guid Id { get; set; }

		/// <summary>名称。</summary>
		public string Name { get; set; } = string.Empty;

		/// <summary>描述。</summary>
		public string? Description { get; set; }

		/// <summary>已就绪的文档数（列表页展示用）。</summary>
		public int ReadyDocumentCount { get; set; }
	}
}
```

```csharp
using Volo.Abp.Application.Services;

namespace KnowVault.Application.Contracts
{
	/// <summary>知识库应用服务（P1 只提供列表，管理界面留 P2）。</summary>
	public interface IKnowledgeBaseAppService : IApplicationService
	{
		/// <summary>列出当前租户可见的知识库。</summary>
		/// <returns>知识库列表，默认知识库排在最前。</returns>
		Task<List<KnowledgeBaseDto>> GetListAsync();

		/// <summary>取默认知识库（前端首屏选中用）。</summary>
		/// <returns>默认知识库；不存在时返回 null。</returns>
		Task<KnowledgeBaseDto?> GetDefaultAsync();
	}
}
```

**Step 3：种子（放 Domain，DbMigrator 会自动发现 `IDataSeedContributor`）**

```csharp
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace KnowVault.Domain
{
	/// <summary>种子一个默认知识库，保证 P1 的 knowledgeBaseId 维度可用。</summary>
	public class KnowledgeBaseDataSeedContributor : IDataSeedContributor, ITransientDependency
	{
		private readonly IRepository<KnowledgeBase, Guid> _repository;

		/// <summary>构造函数。</summary>
		/// <param name="repository">知识库仓储。</param>
		public KnowledgeBaseDataSeedContributor(IRepository<KnowledgeBase, Guid> repository)
		{
			_repository = repository;
		}

		/// <inheritdoc />
		public async Task SeedAsync(DataSeedContext context)
		{
			var existing = await _repository.FindAsync(KnowVaultConsts.DefaultKnowledgeBaseId);
			if (existing is not null)
			{
				return;
			}

			await _repository.InsertAsync(
				new KnowledgeBase(
					KnowVaultConsts.DefaultKnowledgeBaseId,
					"默认知识库",
					"P1 内置的默认知识库，用于承载上传的文档",
					context.TenantId),
				autoSave: true);
		}
	}
}
```

**Step 4：KnowledgeBaseAppService**

```csharp
using KnowVault.Application.Contracts;
using KnowVault.Domain;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Domain.Repositories;

namespace KnowVault.Application
{
	/// <summary>知识库应用服务（P1 只读列表）。</summary>
	[Authorize]
	public class KnowledgeBaseAppService : KnowVaultAppService, IKnowledgeBaseAppService
	{
		private readonly IRepository<KnowledgeBase, Guid> _knowledgeBaseRepository;
		private readonly IRepository<Document, Guid> _documentRepository;

		/// <summary>构造函数。</summary>
		/// <param name="knowledgeBaseRepository">知识库仓储。</param>
		/// <param name="documentRepository">文档仓储（统计就绪数）。</param>
		public KnowledgeBaseAppService(
			IRepository<KnowledgeBase, Guid> knowledgeBaseRepository,
			IRepository<Document, Guid> documentRepository)
		{
			_knowledgeBaseRepository = knowledgeBaseRepository;
			_documentRepository = documentRepository;
		}

		/// <inheritdoc />
		public async Task<List<KnowledgeBaseDto>> GetListAsync()
		{
			var knowledgeBases = await _knowledgeBaseRepository.GetListAsync();
			var documents = await _documentRepository.GetListAsync();

			return knowledgeBases
				.Select(kb => new KnowledgeBaseDto
				{
					Id = kb.Id,
					Name = kb.Name,
					Description = kb.Description,
					ReadyDocumentCount = documents.Count(d => d.KnowledgeBaseId == kb.Id && d.Status == DocumentStatusEnum.Ready)
				})
				// 默认知识库排最前，前端首屏直接选中
				.OrderByDescending(kb => kb.Id == KnowVaultConsts.DefaultKnowledgeBaseId)
				.ThenBy(kb => kb.Name)
				.ToList();
		}

		/// <inheritdoc />
		public async Task<KnowledgeBaseDto?> GetDefaultAsync()
		{
			var list = await GetListAsync();
			return list.FirstOrDefault(kb => kb.Id == KnowVaultConsts.DefaultKnowledgeBaseId) ?? list.FirstOrDefault();
		}
	}
}
```

**Step 5：DocumentAppService：改造为真实上传**

```csharp
using KnowVault.Application.Contracts;
using KnowVault.Domain;
using KnowVault.Domain.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using KnowVault.Ingest;

namespace KnowVault.Application
{
	/// <summary>文档应用服务：上传原件到对象存储 → 落库(Pending) → 发消息触发异步摄入。</summary>
	[Authorize]
	public class DocumentAppService : KnowVaultAppService, IDocumentAppService
	{
		private readonly IRepository<Document, Guid> _documentRepository;
		private readonly IDocumentStorage _documentStorage;
		private readonly IDocumentIngestPublisher _ingestPublisher;
		private readonly IOptions<IngestOptions> _ingestOptions;
		private readonly ILogger<DocumentAppService> _logger;

		/// <summary>构造函数。</summary>
		/// <param name="documentRepository">文档仓储。</param>
		/// <param name="documentStorage">对象存储。</param>
		/// <param name="ingestPublisher">摄入消息发布者。</param>
		/// <param name="ingestOptions">摄入参数（白名单与大小上限）。</param>
		/// <param name="logger">日志。</param>
		public DocumentAppService(
			IRepository<Document, Guid> documentRepository,
			IDocumentStorage documentStorage,
			IDocumentIngestPublisher ingestPublisher,
			IOptions<IngestOptions> ingestOptions,
			ILogger<DocumentAppService> logger)
		{
			_documentRepository = documentRepository;
			_documentStorage = documentStorage;
			_ingestPublisher = ingestPublisher;
			_ingestOptions = ingestOptions;
			_logger = logger;
		}

		/// <inheritdoc />
		public async Task<DocumentDto> UploadAsync(UploadDocumentInput input)
		{
			var fileName = input.File.FileName;
			var extension = Path.GetExtension(fileName).ToLowerInvariant();
			var options = _ingestOptions.Value;

			if (!options.AllowedExtensions.Contains(extension))
			{
				throw new UserFriendlyException($"不支持的文件类型：{extension}（支持 {string.Join("、", options.AllowedExtensions)}）");
			}

			var maxBytes = (long)options.MaxFileSizeMb * 1024 * 1024;
			if (input.File.Length > maxBytes)
			{
				throw new UserFriendlyException($"文件超过 {options.MaxFileSizeMb}MB 上限。");
			}

			var sourceType = IngestOptions.ResolveSourceType(extension)
							 ?? throw new UserFriendlyException($"无法识别文件类型：{extension}");

			var document = new Document(
				GuidGenerator.Create(),
				input.KnowledgeBaseId,
				string.IsNullOrWhiteSpace(input.Title) ? Path.GetFileNameWithoutExtension(fileName) : input.Title!,
				sourceType,
				CurrentTenant.Id);

			await _documentStorage.EnsureBucketAsync();

			// 对象键带租户与知识库前缀，便于按租户清理与排查
			var objectKey = $"{(document.TenantId?.ToString() ?? "host")}/{input.KnowledgeBaseId}/{document.Id}{extension}";
			await using (var stream = await input.File.GetStreamAsync())
			{
				await _documentStorage.PutAsync(objectKey, stream, input.File.ContentType ?? "application/octet-stream");
			}

			document.ObjectStorageKey = objectKey;
			await _documentRepository.InsertAsync(document, autoSave: true);

			await _ingestPublisher.PublishAsync(document.Id, document.TenantId);
			_logger.LogInformation("文档 {DocumentId} 已上传并入队：{ObjectKey}", document.Id, objectKey);

			return MapToDto(document);
		}

		/// <inheritdoc />
		public async Task<DocumentDto> GetAsync(Guid id)
		{
			var document = await _documentRepository.GetAsync(id);
			return MapToDto(document);
		}

		/// <inheritdoc />
		public async Task<List<DocumentDto>> GetListAsync(Guid knowledgeBaseId)
		{
			var documents = await _documentRepository.GetListAsync(x => x.KnowledgeBaseId == knowledgeBaseId);
			return documents.OrderByDescending(d => d.CreationTime).Select(MapToDto).ToList();
		}

		private static DocumentDto MapToDto(Document document) => new()
		{
			Id = document.Id,
			KnowledgeBaseId = document.KnowledgeBaseId,
			Title = document.Title,
			SourceType = document.SourceType,
			Status = document.Status,
			ErrorMessage = document.ErrorMessage,
			ObjectStorageKey = document.ObjectStorageKey,
			CreationTime = document.CreationTime
		};
	}
}
```

接口同步：

```csharp
public interface IDocumentAppService : IApplicationService
{
	/// <summary>上传文档并触发异步摄入。</summary>
	Task<DocumentDto> UploadAsync(UploadDocumentInput input);

	/// <summary>取单个文档（前端轮询状态用）。</summary>
	Task<DocumentDto> GetAsync(Guid id);

	/// <summary>列出知识库下的文档。</summary>
	Task<List<DocumentDto>> GetListAsync(Guid knowledgeBaseId);
}
```

`DocumentDto` 增加 `ObjectStorageKey`；`QaAppService` 类上加 `[Authorize]`（需 `using Microsoft.AspNetCore.Authorization;`）。

**Step 6：验证**

```powershell
dotnet build KnowVault.slnx
```

期望：0 error。此时 API 已需要登录态（`[Authorize]`），前端登录在 Task 12 落地，**中间态接口会返回 401，这是预期的**。

**Step 7：提交** `feat(app): 文档上传到对象存储并入队，增加知识库列表与默认知识库种子`

---

## Task 10：RagService 真实现与引用溯源

**Files:**
- Create: `src/KnowVault.Ai/Rag/RagPromptTemplates.cs`
- Create: `src/KnowVault.Ai/Rag/RagRetrievedChunk.cs`
- Create: `src/KnowVault.Ai/Rag/RagPromptBuilder.cs`
- Modify: `src/KnowVault.Ai/Rag/RagModels.cs`（`RagRequest` 增加 `TenantId`）
- Modify: `src/KnowVault.Ai/Rag/RagService.cs`（真实现）
- Modify: `src/KnowVault.Application/Qa/QaAppService.cs`（传入知识库与租户）
- Test: `tests/KnowVault.Tests/RagPromptBuilderTests.cs`

**Step 1：Prompt 模板（System prompt 是「只依据资料」的硬约束所在）**

```csharp
namespace KnowVault.Ai
{
	/// <summary>RAG 提示词模板。</summary>
	public static class RagPromptTemplates
	{
		/// <summary>系统提示词：限定只依据资料回答、不足就说不知道、标注来源编号。</summary>
		public const string SystemPrompt = """
			你是 KnowVault（智库）的问答助手。你只能依据用户提供的「资料片段」回答问题。

			规则：
			1. 只使用资料片段中的信息作答，禁止用你自己的先验知识补充事实。
			2. 资料片段不足以回答时，直接回答「根据现有资料无法回答该问题」，不要猜测、不要编造。
			3. 每个事实性结论后面用 [编号] 标注来源，例如 [1]、[2][3]；编号必须来自资料片段。
			4. 用简体中文回答，简洁、条理清晰。
			""";
	}
}
```

**Step 2：检索结果模型与 Prompt 组装（纯函数，可测）**

```csharp
namespace KnowVault.Ai
{
	/// <summary>检索命中的切片（已解析出 payload 字段）。</summary>
	public class RagRetrievedChunk
	{
		/// <summary>文档 ID。</summary>
		public Guid DocumentId { get; set; }

		/// <summary>文档标题。</summary>
		public string DocumentTitle { get; set; } = string.Empty;

		/// <summary>切片序号。</summary>
		public int ChunkIndex { get; set; }

		/// <summary>标题层级路径。</summary>
		public string HeadingPath { get; set; } = string.Empty;

		/// <summary>切片正文。</summary>
		public string Content { get; set; } = string.Empty;

		/// <summary>相似度得分。</summary>
		public double Score { get; set; }
	}
}
```

```csharp
using System.Text;

namespace KnowVault.Ai
{
	/// <summary>把检索片段与问题组装成 system/user 两段提示词。</summary>
	public static class RagPromptBuilder
	{
		/// <summary>组装提示词。</summary>
		/// <param name="question">用户问题。</param>
		/// <param name="chunks">检索到的片段（已按相似度降序）。</param>
		/// <returns>system 与 user 提示词。</returns>
		public static (string System, string User) Build(string question, IReadOnlyList<RagRetrievedChunk> chunks)
		{
			var builder = new StringBuilder();
			if (chunks.Count == 0)
			{
				// 明确告知「没有资料」，让模型走「不知道」分支，而不是拿先验知识硬答
				builder.AppendLine("资料片段：没有检索到任何相关资料。");
			}
			else
			{
				builder.AppendLine("资料片段：");
				for (var index = 0; index < chunks.Count; index++)
				{
					var chunk = chunks[index];
					builder.Append('[').Append(index + 1).Append("] 文档《").Append(chunk.DocumentTitle).Append('》');
					if (!string.IsNullOrWhiteSpace(chunk.HeadingPath))
					{
						builder.Append(" · ").Append(chunk.HeadingPath);
					}

					builder.AppendLine();
					builder.AppendLine(chunk.Content);
					builder.AppendLine();
				}
			}

			builder.Append("用户问题：").Append(question);
			return (RagPromptTemplates.SystemPrompt, builder.ToString());
		}
	}
}
```

**Step 3：测试（先红后绿）**

```csharp
using KnowVault.Ai;
using Xunit;

namespace KnowVault.Tests
{
	/// <summary>RAG 提示词组装测试。</summary>
	public class RagPromptBuilderTests
	{
		/// <summary>片段要带 [编号]、文档标题与标题路径。</summary>
		[Fact]
		public void Build_numbers_chunks_with_title_and_heading()
		{
			var chunks = new List<RagRetrievedChunk>
			{
				new() { DocumentTitle = "架构说明", HeadingPath = "第三章 > 3.2", Content = "第一段资料", Score = 0.9 },
				new() { DocumentTitle = "运维手册", HeadingPath = "", Content = "第二段资料", Score = 0.8 }
			};

			var (system, user) = RagPromptBuilder.Build("怎么部署？", chunks);

			Assert.Contains("只依据", system);
			Assert.Contains("[1] 文档《架构说明》 · 第三章 > 3.2", user);
			Assert.Contains("[2] 文档《运维手册》", user);
			Assert.Contains("第一段资料", user);
			Assert.Contains("用户问题：怎么部署？", user);
		}

		/// <summary>没有命中任何片段时必须明说「没有资料」，让模型走不知道分支。</summary>
		[Fact]
		public void Build_when_no_chunk_says_no_material()
		{
			var (_, user) = RagPromptBuilder.Build("无关问题", []);

			Assert.Contains("没有检索到任何相关资料", user);
			Assert.DoesNotContain("资料片段：\n[", user);
		}
	}
}
```

```powershell
dotnet test tests\KnowVault.Tests\KnowVault.Tests.csproj --filter RagPromptBuilderTests
```

期望：实现前编译失败（红）→ 实现后 `Passed`（绿）。

**Step 4：`RagRequest` 增加租户，`RagService` 真实现**

`RagModels.cs` 的 `RagRequest` 增加：

```csharp
		/// <summary>租户 ID（单租户运行时为 Guid.Empty，用于 Qdrant payload 过滤）。</summary>
		public Guid TenantId { get; set; }
```

```csharp
using KnowVault.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace KnowVault.Ai
{
	/// <summary>RAG 编排：问题向量化 → 向量检索 → 组装 Prompt → DeepSeek 生成 → 引用溯源。</summary>
	public class RagService : IRagService, ITransientDependency
	{
		private readonly AiOptions _options;
		private readonly IEmbeddingService _embeddingService;
		private readonly IVectorStore _vectorStore;
		private readonly ILlmChatService _chatService;
		private readonly ILogger<RagService> _logger;

		/// <summary>构造函数。</summary>
		/// <param name="options">AI 配置。</param>
		/// <param name="embeddingService">向量化服务。</param>
		/// <param name="vectorStore">向量库。</param>
		/// <param name="chatService">生成服务。</param>
		/// <param name="logger">日志。</param>
		public RagService(
			IOptions<AiOptions> options,
			IEmbeddingService embeddingService,
			IVectorStore vectorStore,
			ILlmChatService chatService,
			ILogger<RagService> logger)
		{
			_options = options.Value;
			_embeddingService = embeddingService;
			_vectorStore = vectorStore;
			_chatService = chatService;
			_logger = logger;
		}

		/// <inheritdoc />
		public async Task<RagAnswer> AnswerAsync(RagRequest request, CancellationToken cancellationToken = default)
		{
			// top-k 上限 8（TECH_STACK §9：top-k≈5–8）
			var topK = Math.Clamp(request.TopK <= 0 ? _options.TopK : request.TopK, 1, 8);

			var vectors = await _embeddingService.EmbedAsync([request.Question], cancellationToken);
			var hits = await _vectorStore.SearchAsync(
				vectors[0],
				new VectorFilter { TenantId = request.TenantId, KnowledgeBaseId = request.KnowledgeBaseId },
				topK,
				cancellationToken);

			var chunks = hits.Select(hit => new RagRetrievedChunk
			{
				DocumentId = ChunkPayload.ReadGuid(hit.Payload, ChunkPayload.DocumentId),
				DocumentTitle = ChunkPayload.ReadString(hit.Payload, ChunkPayload.DocumentTitle),
				ChunkIndex = ChunkPayload.ReadInt(hit.Payload, ChunkPayload.ChunkIndex),
				HeadingPath = ChunkPayload.ReadString(hit.Payload, ChunkPayload.HeadingPath),
				Content = ChunkPayload.ReadString(hit.Payload, ChunkPayload.Content),
				Score = hit.Score
			}).ToList();

			_logger.LogInformation(
				"检索完成：知识库 {KnowledgeBaseId} 命中 {Count} 个片段（topK={TopK}）",
				request.KnowledgeBaseId, chunks.Count, topK);

			var (system, user) = RagPromptBuilder.Build(request.Question, chunks);
			var answer = await _chatService.CompleteAsync(system, user, cancellationToken);

			return new RagAnswer
			{
				Answer = answer,
				Citations = chunks
					.OrderByDescending(c => c.Score)
					.Select(c => new RagCitation
					{
						DocumentId = c.DocumentId,
						Title = c.DocumentTitle,
						ChunkIndex = c.ChunkIndex,
						Score = c.Score
					})
					.ToList()
			};
		}
	}
}
```

**Step 5：`QaAppService` 传参**

`_ragService.AnswerAsync(new RagRequest { … })` 增加：

```csharp
				TenantId = CurrentTenant.Id ?? Guid.Empty,
				KnowledgeBaseId = input.KnowledgeBaseId ?? KnowVaultConsts.DefaultKnowledgeBaseId,
				TopK = input.TopK ?? 0,
```

（`AskQuestionInput` 同步增加 `public int? TopK { get; set; }`；`using KnowVault.Domain;` 已在文件里。）

**Step 6：验证与提交**

```powershell
dotnet test tests\KnowVault.Tests\KnowVault.Tests.csproj
```

期望：全部 `Passed`。提交：`feat(ai): RagService 接入真实检索与 DeepSeek 生成并返回引用`

---

## Task 11：Host 与 AppHost 装配（端口 / 密钥 / 引用）

**Files:**
- Modify: `aspire/KnowVault.AppHost/Program.cs`
- Modify: `src/KnowVault.HttpApi.Host/appsettings.json`（Task 1 已加节，这里补 `AuthServer` 与端口对齐说明）
- Modify: `src/KnowVault.HttpApi.Host/KnowVaultHttpApiHostModule.cs`（无需新增依赖，确认 Ingest 模块已在 `DependsOn` 中）
- Create: `src/KnowVault.DbMigrator/appsettings.secrets.json`（若无则按现有 `.example` 复制；**已存在，勿覆盖**）

**Step 1：AppHost 关键改动（把每个坑都钉死）**

```csharp
var builder = DistributedApplication.CreateBuilder(args);

// .env / 进程环境变量 → 后端配置键的映射（Key 绝不写进仓库）
static string? Env(string name) => Environment.GetEnvironmentVariable(name);

// ---------- 依赖服务 ----------

var postgres = builder.AddPostgres("postgres")
    .WithImageTag("16")
    .WithDataVolume("kv-postgres-data");

var database = postgres.AddDatabase("KnowVault");

var redis = builder.AddRedis("redis")
    .WithDataVolume("kv-redis-data");

var qdrant = builder.AddQdrant("qdrant")
    .WithImage("qdrant/qdrant")
    .WithDataVolume("kv-qdrant-data");

var rabbitmq = builder.AddRabbitMQ("rabbitmq")
    .WithImage("rabbitmq:3-management")
    .WithDataVolume("kv-rabbitmq-data");

var seq = builder.AddSeq("seq");

// MinIO：官方镜像必须显式给 server 子命令，否则容器起不来；控制台端口 9001
var minio = builder.AddContainer("minio", "minio/minio", "latest")
    .WithArgs("server", "/data", "--console-address", ":9001")
    .WithEnvironment("MINIO_ROOT_USER", Env("MINIO_ROOT_USER") ?? "minioadmin")
    .WithEnvironment("MINIO_ROOT_PASSWORD", Env("MINIO_ROOT_PASSWORD") ?? "minioadmin")
    .WithVolume("kv-minio-data", "/data")
    .WithEndpoint(9000, 9000, "http")
    .WithEndpoint(9001, 9001, "http");

// ---------- 应用 ----------
var api = builder.AddProject("knowvault-api", "../../src/KnowVault.HttpApi.Host/KnowVault.HttpApi.Host.csproj")
    // 端口必须与 appsettings 的 App:SelfUrl / AuthServer:Authority 一致，否则 OpenIddict issuer 校验失败
    .WithHttpsEndpoint(port: 44300, name: "https")
    .WithReference(database, "ConnectionStrings:Default")
    .WithReference(redis)
    .WithReference(rabbitmq)          // 注入 ConnectionStrings:rabbitmq（amqp://...）
    .WithEnvironment("App:CorsOrigins", "http://localhost:5173")
    // QdrantVectorStore 走 gRPC，必须注入 6334 的 grpc 端点（不是 6333 的 REST 端点）
    .WithEnvironment("Qdrant__Endpoint", qdrant.GetEndpoint("grpc"))
    .WithEnvironment("Minio__Endpoint", "minio:9000")
    .WithEnvironment("Minio__AccessKey", Env("MINIO_ROOT_USER") ?? "minioadmin")
    .WithEnvironment("Minio__SecretKey", Env("MINIO_ROOT_PASSWORD") ?? "minioadmin")
    .WithEnvironment("Minio__UseSsl", "false")
    .WithEnvironment("Ai__DeepSeekApiKey", Env("DEEPSEEK_API_KEY") ?? string.Empty)
    .WithEnvironment("Ai__DeepSeekBaseUrl", Env("DEEPSEEK_BASE_URL") ?? "https://api.deepseek.com")
    .WithEnvironment("Ai__DeepSeekModel", Env("DEEPSEEK_MODEL") ?? "deepseek-chat")
    .WithEnvironment("Ai__QwenApiKey", Env("QWEN_API_KEY") ?? string.Empty)
    .WithEnvironment("Ai__QwenBaseUrl", Env("QWEN_BASE_URL") ?? "https://dashscope.aliyuncs.com/compatible-mode/v1")
    .WithEnvironment("Ai__QwenEmbeddingModel", Env("QWEN_EMBEDDING_MODEL") ?? "text-embedding-v3");

builder.Build().Run();
```

> **两个必须现场核对点**（写进执行记录，别猜）：
> 1. `qdrant.GetEndpoint("grpc")` 的端点名需在 Aspire 面板里核对：若资源只暴露 `http`（6333），则改用该端点并在 `QdrantOptions.Endpoint` 上保证协议/端口正确——**Qdrant 的 gRPC 端口是 6334**。
> 2. MinIO 的 `Endpoint` 是**容器内主机名**（`minio:9000`）——API 与 MinIO 同在 Aspire 容器网络里时可用；若连不上，改成 `localhost:9000`（AppHost 在宿主机跑 API 进程时用宿主端口）。

**Step 2：MinIO 密钥进 `.env`**

`.env`（本地，**不入库**）里加：

```dotenv
MINIO_ROOT_USER=minioadmin
MINIO_ROOT_PASSWORD=minioadmin
```

**Step 3：启动验证**

```powershell
# 终端 A：起依赖与应用（首次会拉镜像，较慢）
dotnet run --project aspire\KnowVault.AppHost
```

逐项确认：

```powershell
# 1) API 是否在 44300 且健康检查可用（-SkipCertificateCheck 因自签证书）
Invoke-RestMethod -Uri https://localhost:44300/health-status -SkipCertificateCheck

# 2) Qdrant collection 是否被后台初始化创建
Invoke-RestMethod -Uri http://localhost:6333/collections/knowvault_chunks

# 3) RabbitMQ 队列是否因 consumer 启动而创建（Management API）
Invoke-RestMethod -Uri http://localhost:15672/api/queues -Headers @{ Authorization = 'Basic ' + [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes('guest:guest')) } | Select-Object -ExpandProperty name
```

期望：健康检查返回 `Healthy`；Qdrant 返回 collection 配置（`size: 1024`、`distance: Cosine`）；RabbitMQ 队列列表里出现 consumer 的队列（`document-ingest` 或带命名空间前缀的队列名）。

**Step 4：提交** `chore(host): 钉住 API 端口并串联 Qdrant/MinIO/RabbitMQ 与模型密钥注入`

---

## Task 12：前端登录与 Bearer 鉴权

**Files:**
- Modify: `frontend/vite.config.js`（`/connect` 代理）
- Create: `frontend/src/api/auth.js`
- Create: `frontend/src/stores/auth.js`
- Create: `frontend/src/components/LoginPanel.vue`
- Modify: `frontend/src/api/http.js`（请求注入 Bearer、401 清理）
- Modify: `frontend/src/App.vue`（未登录显示登录页）
- Modify: `frontend/src/main.js`（必要时无需改动，确认 Pinia 已装载）

**Step 0（**关键前置核实**）：确认 `KnowVault_App` 客户端真的存在**

`src/KnowVault.DbMigrator/appsettings.json` 里 `KnowVault_App` **没有 `RootUrl`**，而 `OpenIddictDataSeedContributor` 会把它作为唯一 redirect URI 传给 OpenIddict；OpenIddict 对 redirect URI 做绝对 URI 校验，**null 会让种子失败**。先查库确认：

```powershell
docker exec -it $(docker ps --filter "name=postgres" --format "{{.Names}}" | Select-Object -First 1) psql -U postgres -d KnowVault -c "select \"ClientId\" from \"OpenIddictApplications\";"
```

- 若已存在 `KnowVault_App` → 继续；
- 若不存在或 DbMigrator 报错 → 在 `src/KnowVault.DbMigrator/appsettings.json` 的 `KnowVault_App` 下补 `"RootUrl": "https://localhost:44300"`，重跑 `dotnet run --project src/KnowVault.DbMigrator`。

**Step 1：`vite.config.js` 增加 `/connect` 代理**

```js
import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

// KnowVault 前端（Vue 3 + JS）。开发时把 /api 与 /connect 代理到后端：
// - /api     业务接口
// - /connect OpenIddict 令牌端点（密码模式换 token），必须同源代理，否则 CORS/证书双重麻烦
export default defineConfig({
  plugins: [vue()],
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: process.env.VITE_API_TARGET || 'https://localhost:44300',
        changeOrigin: true,
        secure: false,
      },
      '/connect': {
        target: process.env.VITE_API_TARGET || 'https://localhost:44300',
        changeOrigin: true,
        secure: false,
      },
    },
  },
})
```

**Step 2：`src/api/auth.js`（注意：令牌端点不在 `/api` 下，必须用独立 axios 调用）**

```js
import axios from 'axios'

// OpenIddict 令牌端点：走 Vite 的 /connect 代理，避免跨域与自签证书问题。
const TOKEN_URL = '/connect/token'
const CLIENT_ID = 'KnowVault_App'

/**
 * 用资源所有者密码模式换取访问令牌。
 * @param {{username: string, password: string}} credentials 登录凭据
 * @returns {Promise<{accessToken: string, refreshToken: string|null, expiresIn: number}>} 令牌信息
 */
export async function login({ username, password }) {
  const body = new URLSearchParams({
    grant_type: 'password',
    client_id: CLIENT_ID,
    username,
    password,
    scope: 'openid profile email roles KnowVault offline_access',
  })

  const { data } = await axios.post(TOKEN_URL, body, {
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
  })

  return {
    accessToken: data.access_token,
    refreshToken: data.refresh_token || null,
    expiresIn: data.expires_in,
  }
}
```

**Step 3：`src/stores/auth.js`**

```js
import { defineStore } from 'pinia'
import { login as loginApi } from '../api/auth'

const TOKEN_KEY = 'knowvault.token'

// 登录态：token 存 localStorage，刷新页面不丢；P2 再做 refresh token 自动续期。
export const useAuthStore = defineStore('auth', {
  state: () => ({
    token: localStorage.getItem(TOKEN_KEY) || '',
    loading: false,
    error: '',
  }),

  getters: {
    /** 是否已登录。 @returns {boolean} */
    isAuthenticated: (state) => !!state.token,
  },

  actions: {
    /**
     * 登录并保存令牌。
     * @param {{username: string, password: string}} credentials 登录凭据
     * @returns {Promise<boolean>} 是否登录成功
     */
    async login(credentials) {
      this.loading = true
      this.error = ''
      try {
        const { accessToken } = await loginApi(credentials)
        this.token = accessToken
        localStorage.setItem(TOKEN_KEY, accessToken)
        return true
      } catch (e) {
        // OpenIddict 失败时返回 { error: 'invalid_grant', error_description: '...' }
        this.error = e?.response?.data?.error_description || e?.message || '登录失败'
        return false
      } finally {
        this.loading = false
      }
    },

    /** 退出登录并清理本地令牌。 */
    logout() {
      this.token = ''
      localStorage.removeItem(TOKEN_KEY)
    },
  },
})
```

**Step 4：`src/api/http.js` 注入 Bearer 并处理 401**

```js
import axios from 'axios'

const TOKEN_KEY = 'knowvault.token'

// 统一 axios 实例：baseURL=/api（开发时由 Vite 代理到后端）。
export const http = axios.create({
  baseURL: '/api',
  timeout: 60000,   // 问答链路要调两次外部模型，30s 偏紧
})

http.interceptors.request.use((config) => {
  const token = localStorage.getItem(TOKEN_KEY)
  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }
  return config
})

http.interceptors.response.use(
  (res) => res.data,
  (err) => {
    // 401：令牌缺失/过期 → 清掉本地令牌，让界面回到登录页
    if (err?.response?.status === 401) {
      localStorage.removeItem(TOKEN_KEY)
      window.dispatchEvent(new CustomEvent('knowvault:unauthorized'))
    }
    console.error('[KnowVault] 请求错误', err)
    return Promise.reject(err)
  },
)
```

**Step 5：`src/components/LoginPanel.vue`**

```vue
<template>
  <div class="login-wrap">
    <n-card title="KnowVault · 智库" class="login-card">
      <n-form :disabled="auth.loading" @submit.prevent="submit">
        <n-form-item label="用户名">
          <n-input v-model:value="username" placeholder="admin" @keydown.enter.prevent="submit" />
        </n-form-item>
        <n-form-item label="密码">
          <n-input v-model:value="password" type="password" show-password-on="click" @keydown.enter.prevent="submit" />
        </n-form-item>
        <n-alert v-if="auth.error" type="error" :show-icon="false" style="margin-bottom: 12px">
          {{ auth.error }}
        </n-alert>
        <n-button type="primary" block :loading="auth.loading" @click="submit">登录</n-button>
      </n-form>
    </n-card>
  </div>
</template>

<script setup>
import { ref } from 'vue'
import { NCard, NForm, NFormItem, NInput, NButton, NAlert } from 'naive-ui'
import { useAuthStore } from '../stores/auth'

const auth = useAuthStore()
const username = ref('')
const password = ref('')

// 提交登录：成功后父组件会依据 isAuthenticated 切换到主界面
async function submit() {
  if (!username.value || !password.value) return
  await auth.login({ username: username.value, password: password.value })
}
</script>

<style scoped>
.login-wrap { display: flex; align-items: center; justify-content: center; height: 100vh; background: #f5f7fa; }
.login-card { width: 380px; }
</style>
```

**Step 6：`App.vue` 按登录态切换**

```vue
<template>
  <n-config-provider :theme-overrides="themeOverrides" :locale="zhCN" :date-locale="dateZhCN">
    <n-message-provider>
      <LoginPanel v-if="!auth.isAuthenticated" />
      <n-layout v-else style="height: 100vh">
        <n-layout-header bordered style="padding: 16px 24px; display: flex; align-items: center; gap: 12px">
          <span style="font-weight: 700; font-size: 18px">KnowVault · 智库</span>
          <n-tag :type="chat.connected ? 'success' : 'default'" size="small" round>
            后端 {{ chat.connected ? '已连接' : '未连接' }}
          </n-tag>
          <div style="margin-left: auto">
            <n-button size="small" quaternary @click="logout">退出</n-button>
          </div>
        </n-layout-header>
        <n-layout-content content-style="padding: 24px; height: 100%">
          <ChatPanel />
        </n-layout-content>
      </n-layout>
    </n-message-provider>
  </n-config-provider>
</template>

<script setup>
import { onMounted, onUnmounted } from 'vue'
import {
  NConfigProvider, NLayout, NLayoutHeader, NLayoutContent, NMessageProvider, NTag, NButton,
  zhCN, dateZhCN,
} from 'naive-ui'
import ChatPanel from './components/ChatPanel.vue'
import LoginPanel from './components/LoginPanel.vue'
import { useChatStore } from './stores/chat'
import { useAuthStore } from './stores/auth'

const chat = useChatStore()
const auth = useAuthStore()

const themeOverrides = { common: { primaryColor: '#2080f0' } }

function logout() {
  auth.logout()
  chat.messages = []
}

// 全局 401 事件：令牌失效时退回登录页
function onUnauthorized() {
  auth.logout()
}
onMounted(() => {
  window.addEventListener('knowvault:unauthorized', onUnauthorized)
  if (auth.isAuthenticated) chat.checkConnection()
})
onUnmounted(() => window.removeEventListener('knowvault:unauthorized', onUnauthorized))
</script>

<style>
html, body, #app { margin: 0; height: 100%; }
</style>
```

**Step 7：验证**

```powershell
cd frontend; npm run dev
```

1. 浏览器 `http://localhost:5173` → 应看到登录页；
2. 用 ABP 种子账号登录：用户名 `admin`，密码 `1q2w3E*`（来自 `KnowVaultConsts.AdminPasswordDefaultValue`）。**若报 `invalid_grant`，改试 `admin@abp.io`**（ABP 不同版本对用户名/邮箱的匹配行为有差异）；
3. 登录后刷新页面应保持登录态；
4. 匿名访问 `https://localhost:44300/api/app/documents?knowledgeBaseId=...` 应返回 401（确认 `[Authorize]` 生效）。

**Step 8：提交** `feat(web): 增加登录页与 Bearer 鉴权接入`

---

## Task 13：前端上传、文档状态与知识库下拉

**Files:**
- Create: `frontend/src/api/documents.js`
- Create: `frontend/src/api/knowledgeBases.js`
- Create: `frontend/src/stores/documents.js`
- Create: `frontend/src/components/DocumentPanel.vue`
- Modify: `frontend/src/stores/chat.js`（带 knowledgeBaseId）
- Modify: `frontend/src/components/ChatPanel.vue`（引用展示增强）
- Modify: `frontend/src/App.vue`（左右分栏）

**Step 1：API 封装**

```js
import { http } from './http'

// 文档接口（ABP 动态 API：/api/app/documents）
export const documentApi = {
  /**
   * 上传文档。
   * @param {{knowledgeBaseId: string, title?: string, file: File}} payload 上传参数
   * @returns {Promise<object>} 新建的文档 DTO
   */
  upload: ({ knowledgeBaseId, title, file }) => {
    const form = new FormData()
    form.append('knowledgeBaseId', knowledgeBaseId)
    if (title) form.append('title', title)
    form.append('file', file)
    return http.post('/app/documents/upload', form, {
      headers: { 'Content-Type': 'multipart/form-data' },
    })
  },

  /**
   * 按知识库列文档。
   * @param {string} knowledgeBaseId 知识库 ID
   * @returns {Promise<object[]>} 文档列表
   */
  list: (knowledgeBaseId) => http.get('/app/documents', { params: { knowledgeBaseId } }),
}
```

```js
import { http } from './http'

// 知识库接口（P1 只有列表与默认库）
export const knowledgeBaseApi = {
  /** 列出知识库。 @returns {Promise<object[]>} 知识库列表 */
  list: () => http.get('/app/knowledge-base'),

  /** 取默认知识库。 @returns {Promise<object>} 默认知识库 */
  getDefault: () => http.get('/app/knowledge-base/default'),
}
```

> 路径以 ABP 动态 API 约定为准：`KnowledgeBaseAppService` → `/api/app/knowledge-base`。启动后在 Swagger 里核对一遍实际路径，**以 Swagger 为准**。

**Step 2：`stores/documents.js`（含状态轮询）**

```js
import { defineStore } from 'pinia'
import { documentApi } from '../api/documents'
import { knowledgeBaseApi } from '../api/knowledgeBases'

// 文档与知识库状态。轮询只在存在未完成文档时进行，避免空转。
export const useDocumentStore = defineStore('documents', {
  state: () => ({
    knowledgeBases: [],
    knowledgeBaseId: '',
    documents: [],
    uploading: false,
    polling: false,
    error: '',
  }),

  actions: {
    /** 初始化：取知识库列表并选中默认知识库。 */
    async init() {
      try {
        this.knowledgeBases = await knowledgeBaseApi.list()
        this.knowledgeBaseId = this.knowledgeBases[0]?.id || ''
        await this.refresh()
      } catch (e) {
        this.error = e?.response?.data?.error?.message || e?.message || '加载知识库失败'
      }
    },

    /** 拉取当前知识库的文档列表。 */
    async refresh() {
      if (!this.knowledgeBaseId) return
      this.documents = await documentApi.list(this.knowledgeBaseId)
      this.ensurePolling()
    },

    /**
     * 上传文件并刷新列表。
     * @param {File} file 待上传文件
     */
    async upload(file) {
      this.uploading = true
      this.error = ''
      try {
        await documentApi.upload({ knowledgeBaseId: this.knowledgeBaseId, file })
        await this.refresh()
      } catch (e) {
        this.error = e?.response?.data?.error?.message || e?.message || '上传失败'
      } finally {
        this.uploading = false
      }
    },

    /** 有 Pending/Processing 文档时每 2s 轮询一次（P1 没有 SignalR 推送）。 */
    ensurePolling() {
      const pending = this.documents.some((d) => d.status === 0 || d.status === 1)
      if (pending && !this.polling) {
        this.polling = true
        const timer = setInterval(async () => {
          await this.refresh()
          if (!this.documents.some((d) => d.status === 0 || d.status === 1)) {
            clearInterval(timer)
            this.polling = false
          }
        }, 2000)
      }
    },
  },
})
```

> ⚠️ `status` 是枚举 int 落库：`Pending=0 / Processing=1 / Ready=2 / Failed=3`（见 `DocumentStatusEnum`）。前端用数值判断，**不要**按字符串比较。

**Step 3：`DocumentPanel.vue`（上传 + 状态表）**

```vue
<template>
  <div class="doc-panel">
    <div class="row">
      <n-select
        v-model:value="store.knowledgeBaseId"
        :options="kbOptions"
        size="small"
        @update:value="store.refresh()"
      />
    </div>

    <n-upload :custom-request="customRequest" :show-file-list="false" :disabled="store.uploading">
      <n-button size="small" type="primary" :loading="store.uploading" block>上传文档（PDF/Word/Excel/MD/HTML）</n-button>
    </n-upload>

    <n-alert v-if="store.error" type="error" :show-icon="false" closable @close="store.error = ''">
      {{ store.error }}
    </n-alert>

    <n-list bordered>
      <n-list-item v-for="doc in store.documents" :key="doc.id">
        <div class="doc-row">
          <span class="doc-title" :title="doc.title">{{ doc.title }}</span>
          <n-tag size="tiny" :type="statusType(doc.status)">{{ statusText(doc.status) }}</n-tag>
        </div>
        <div v-if="doc.status === 3 && doc.errorMessage" class="doc-error">{{ doc.errorMessage }}</div>
      </n-list-item>
      <n-empty v-if="store.documents.length === 0" description="还没有文档" />
    </n-list>
  </div>
</template>

<script setup>
import { computed } from 'vue'
import { NSelect, NUpload, NButton, NList, NListItem, NTag, NAlert, NEmpty, useMessage } from 'naive-ui'
import { useDocumentStore } from '../stores/documents'

const store = useDocumentStore()
const message = useMessage()

const kbOptions = computed(() => store.knowledgeBases.map((kb) => ({ label: kb.name, value: kb.id })))

// 状态枚举：0 Pending / 1 Processing / 2 Ready / 3 Failed
const statusText = (status) => ['待处理', '解析中', '已就绪', '失败'][status] ?? '未知'
const statusType = (status) => ['default', 'info', 'success', 'error'][status] ?? 'default'

/**
 * n-upload 的自定义上传：交给 store 走我们自己的 axios 实例（带 Bearer）。
 * @param {{file: {file: File}, onFinish: Function, onError: Function}} options 上传回调参数
 */
async function customRequest({ file, onFinish, onError }) {
  try {
    await store.upload(file.file)
    message.success('已提交解析')
    onFinish()
  } catch (e) {
    onError()
  }
}
</script>

<style scoped>
.doc-panel { display: flex; flex-direction: column; gap: 12px; }
.row { display: flex; gap: 8px; }
.doc-row { display: flex; align-items: center; justify-content: space-between; gap: 8px; }
.doc-title { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.doc-error { color: #d03050; font-size: 12px; margin-top: 4px; word-break: break-all; }
</style>
```

**Step 4：`stores/chat.js` 带知识库提问**

`ask` 改为：

```js
    async ask(question, knowledgeBaseId) {
      this.loading = true
      this.messages.push({ role: 'user', content: question })
      try {
        const res = await qaApi.ask({ question, knowledgeBaseId: knowledgeBaseId || undefined })
        this.messages.push({ role: 'assistant', content: res.answer, citations: res.citations || [] })
      } catch (e) {
        this.messages.push({
          role: 'assistant',
          content: '请求失败：' + (e?.response?.data?.error?.message || e?.message || e),
        })
      } finally {
        this.loading = false
      }
    },
```

`ChatPanel.vue` 的 `send()` 传入 `documentStore.knowledgeBaseId`；引用展示改为「文档标题 · 片段 N · 相似度」。

**Step 5：`App.vue` 左右分栏**

```vue
        <n-layout-content content-style="padding: 24px; height: 100%">
          <div class="layout">
            <aside class="side"><DocumentPanel /></aside>
            <main class="main"><ChatPanel /></main>
          </div>
        </n-layout-content>
```

```css
.layout { display: flex; gap: 24px; height: 100%; }
.side { width: 340px; flex: none; }
.main { flex: 1; min-width: 0; }
```

`onMounted` 里登录后调用 `documentStore.init()`。

**Step 6：验证**

1. 上传一个 Markdown 文件 → 列表出现「待处理 → 解析中 → 已就绪」；
2. 上传一个 PDF → 同样走完流程；
3. 故意上传 `.txt` → 前端提示「不支持的文件类型」。

**Step 7：提交** `feat(web): 增加文档上传、状态轮询与知识库下拉`

---

## Task 14：端到端验收与收尾

**Step 1：按 §6 验收清单逐条执行（P1 的 Done 定义）**

| # | 验收项 | 操作 | 期望 |
|---|---|---|---|
| 1 | 一键起全栈 | `dotnet run --project aspire/KnowVault.AppHost` | 7 个资源全部 Running，API 在 44300 |
| 2 | 登录 | `http://localhost:5173` → admin / `1q2w3E*` | 进入主界面，刷新保持登录 |
| 3 | 上传 5 种格式 | PDF、docx、xlsx、md、html 各一份 | 全部走到「已就绪」 |
| 4 | MinIO 有原件 | 打开 `http://localhost:9001`（minioadmin/minioadmin） | `knowvault` bucket 内对象键为 `host/{kbId}/{docId}.xxx` |
| 5 | Qdrant 有向量 | `Invoke-RestMethod http://localhost:6333/collections/knowvault_chunks` | `points_count` = 各文档切片数之和；抽查一个 point 的 payload 含 8 个字段且 `tenantId` = `00000000-0000-0000-0000-000000000000` |
| 6 | Postgres 有切片 | `select count(*) from "AppDocumentChunks";` | 与 Qdrant points 数一致 |
| 7 | 问答 + 引用 | 就某个上传文档的内容提问 | 答案正确、末尾带 `[n]`，界面显示「文档标题 · 片段 N · 相似度」 |
| 8 | 不知道分支 | 问一个资料里完全没有的问题 | 回答「根据现有资料无法回答该问题」，**不编造** |
| 9 | 负向：Key 错 | 临时把 `QWEN_API_KEY` 改成错值重启，上传新文档 | 文档落「失败」并显示错误信息，服务不崩 |
| 10 | 静态检查 | `dotnet build` + `dotnet format style --verify-no-changes --diagnostics IDE1006` | 0 error；枚举后缀合规 |
| 11 | 行尾 | 对本次新增/改动的文本文件跑行尾自查 | 每个文件 CRLF 与 loneLF 有一个为 0（`AGENTS.md` 为 LF 例外） |

**Step 2：把验收结论与踩到的坑写回文档**

- `TECH_STACK.md` §9.1 追加「实测结论」（Qdrant 端点名、MinIO endpoint 取值、DashScope 批大小上限实测值）；
- `AGENTS.md` §七 更新待办（哪些 P1 已完成、哪些坑留给 P2）；
- 若实现与规格有偏差（例如批大小、切片单位），**先改 §0/§9.1 再改代码**。

**Step 3：收尾提交**

```powershell
git -C . add -A
git -C . commit -m "feat: 打通 P1 最小问答闭环（上传→解析→切片→向量化→检索→生成→引用）"
```

---

## §2 风险登记（执行时逐条盯着）

| # | 风险 | 触发条件 | 应对 |
|---|---|---|---|
| 1 | MassTransit v9 商业许可 | 有人顺手升到 9.x | 锁 8.5.7；升级前必须先确认许可 |
| 2 | OpenIddict issuer 不匹配 | AppHost 用动态端口 | 钉 44300（Task 11） |
| 3 | `KnowVault_App` 未成功种子 | DbMigrator 因空 RootUrl 失败 | Task 12 Step 0 先查库，必要时补 RootUrl 重跑 |
| 4 | DashScope 批大小/维度与假设不符 | embedding 返回 400 或维度不是 1024 | 以实测为准改 `MaxBatchSize` / `QdrantOptions.VectorSize`，并同步 `TECH_STACK` |
| 5 | Qdrant 端点取成 REST(6333) | `Qdrant__Endpoint` 配错 | `QdrantVectorStore` 走 gRPC，需 6334 |
| 6 | MinIO endpoint 主机名不通 | API 进程在宿主机、MinIO 在容器 | 改用 `localhost:9000` |
| 7 | Qdrant 无法 match null 租户 | 单租户下 `TenantId` 为 null | 写入侧归一化 `Guid.Empty`（Task 4/8 已实现） |
| 8 | 切片的 `status` 数值判断写错 | 前后端枚举理解不一致 | 前端按 0/1/2/3 数值判断（Task 13 已注明） |
| 9 | 超长文档 embedding 超时 | 一次几万个切片 | 已按 10 条分批；超大文档留 P2 做流式分批与进度 |
| 10 | PDF 无法自动化测试 | 缺可复现二进制夹具 | 手工验收（验收项 3）+ P3 补 fixtures 约定 |

## §3 完成定义（DoD）

- §6 的 11 项验收全绿，且负向用例（Key 错、资料外提问）表现符合预期；
- `dotnet build` 0 error、`dotnet test` 全绿、`IDE1006` 无告警；
- 新增/改动文本文件行尾合规（CRLF，`AGENTS.md` 例外）；
- `TECH_STACK.md` 与 `AGENTS.md` 已按 Task 0/14 回写，且与实际实现一致；
- 每个 Task 一个 Conventional Commits 提交，历史可读。
