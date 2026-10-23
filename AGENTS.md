# AGENTS.md — KnowVault 项目工作区指令

> 本文件是 DSH / AI agent **自动读取**的项目规范文件（由 `@deepseek-ai/dsh-agent-instructions` 注入为工作区指令）。
> 规则优先级：具体的指令 > 宽泛的指令；本文件不覆盖 system / developer / 用户直接指令。
> 技术选型、架构、数据模型、路线图的**权威定义**见同目录 `TECH_STACK.md`，两者共同构成项目说明书。

---

## 一、项目概述

**KnowVault（智库）** —— 一个 **多租户 RAG 知识库**（当前先以单租户运行）。

> 你问，它从你的文档里找答案。

- 用户上传私有文档（PDF/Word/Excel/Markdown/网页）。
- 系统**异步解析 → 切片 → 向量化**，写入向量库 **Qdrant**。
- 用户提问 → **语义检索** → 组装 Prompt → **LLM 生成** + **引用溯源**。

### 核心技术底座
| 域 | 技术 |
|---|---|
| 后端 | .NET 10 (LTS) + ABP.VNext + Semantic Kernel |
| 数据 | PostgreSQL + EF Core ｜ Redis ｜ MinIO ｜ Qdrant |
| 异步 | RabbitMQ / MassTransit 8.5.7（Apache-2.0；**9.x 为商业许可，勿用**） |
| AI 模型 | **DeepSeek**（生成） + **SiliconFlow**（Embedding + Rerank，跨厂商路由） |
| 前端 | Vue 3 + Vite + **JavaScript**（不用 TS）+ Naive UI + Pinia |
| 本地编排 | .NET Aspire｜Docker Compose |
| 可观测 | OpenTelemetry + Seq + Serilog |
| 测试 | xUnit + Testcontainers + FluentAssertions（**必须钉 7.x**；8.x 已改为商业许可） |

---

## 二、项目结构

```
KnowVault.slnx
├─ src/
│  ├─ KnowVault.Domain.Shared         // 共享枚举 / 常量 / 本地化资源（跨层复用类型）
│  ├─ KnowVault.Domain                // 实体 / 聚合 / 仓储接口（多租户预留）
│  ├─ KnowVault.Application.Contracts // AppService 契约 + DTO
│  ├─ KnowVault.Application           // AppService 用例编排 + AI 编排入口
│  ├─ KnowVault.EntityFrameworkCore   // EF Core(PostgreSQL) + 迁移；Qdrant 与 MinIO 访问实现（接口定义在 Domain）
│  ├─ KnowVault.Ai                   // Semantic Kernel：Prompt / RAG / 向量检索 / 重排 / 流式
│  ├─ KnowVault.Ingest               // 文档解析与摄入（P1：进程内 MassTransit consumer，消费 RabbitMQ，产出切片+向量；P3 可拆独立 Worker）
│  ├─ KnowVault.HttpApi              // 动态 API 控制器（ABP 自动生成）+ SignalR Hub（P2）
│  ├─ KnowVault.HttpApi.Client       // HttpClient 强类型代理（供外部调用方使用）
│  ├─ KnowVault.HttpApi.Host         // 主宿主（OpenIddict/健康检查/Swagger），Aspire 编排入口
│  └─ KnowVault.DbMigrator           // 数据库迁移 / 种子执行器（EF 迁移入口）
├─ aspire/
│  └─ KnowVault.AppHost              // Aspire 编排：Postgres/Redis/Qdrant/MinIO/RabbitMQ/Seq + API
├─ frontend/                          // Vue3 + Vite + JS + Naive UI
└─ tests/                             // KnowVault.Tests（纯函数单测）+ KnowVault.IntegrationTests（P3 Testcontainers）
```

> **当前实现（P0 ABP 分层模板）**：主宿主为 ABP 约定的 `KnowVault.HttpApi.Host`（非 `KnowVault.Web`），并含 `Domain.Shared` / `Application.Contracts` / `HttpApi.Client` / `DbMigrator` 等标准层。**P1 起** `tests/` 落地 `KnowVault.Tests`（纯函数单测）；**P3** 新增 `tests/KnowVault.IntegrationTests`（Testcontainers 四件套）。技术选型 / 架构 / 数据模型 / 路线图的权威定义见 `TECH_STACK.md`，P1 的冻结规格与执行计划见 `docs/plans/2026-10-05-knowvault-p1-mvp.md`；**P2** 的冻结规格与执行计划见 `docs/plans/2026-10-14-knowvault-p2-mvp.md`（流式 Hub 在 `src/KnowVault.HttpApi/Hubs/`，重排与流式实现在 `src/KnowVault.Ai/Rerank/` 与 `src/KnowVault.Ai/Chat/`）；**P3** 见 `docs/plans/2026-10-18-knowvault-p3-mvp.md`（OTel/Seq、角色+知识库 ACL、文档删除/重处理、摄入重试、Testcontainers、CI）。

---

## 三、语言与风格约定

### 后端（C# / .NET / ABP）
- 语言版本随 .NET 10（C# 14），`Nullable` 开启。
- 遵循 **ABP 约定**：应用服务命名以 `AppService` 结尾；实体继承 `Entity` / `AggregateRoot`；公共方法遵循 .NET 异步命名（`XXXAsync`）。
- **枚举命名必须带 `Enum` 后缀（强制执行）**：所有 `enum` 类型名一律以 `Enum` 结尾，**文件名与类型名保持一致**（一个 `.cs` 文件一个枚举），引用处同样使用带后缀的类型名。例如 `src/KnowVault.Domain.Shared/Enum/Chat/MessageRoleEnum.cs` → `public enum MessageRoleEnum`，使用处写 `MessageRoleEnum.User`；`DocumentStatusEnum` / `DocumentSourceTypeEnum` 同理。`.editorconfig` 已对 `enum` 声明启用命名规则（`required_suffix = Enum`），违规命中 **IDE1006**：VS 会实时提示，`dotnet format style --verify-no-changes --diagnostics IDE1006` 可检出（注意命名规则**不参与 `dotnet build`**，即使开了 `EnforceCodeStyleInBuild`，所以别指望构建帮你拦），不要写 `public enum MessageRole` 这类无后缀枚举。
- **分层职责清晰**：Domain 不放 EF/DTO；Application 做用例编排，不含 SQL；EF Core 只做持久化；AI 逻辑只放 `KnowVault.Ai`。
- 所有业务实体**预留多租户**：表带 `TenantId`（ABP 一等公民，先单租户运行不用强制隔离，但结构保留）。
- 主键用 GUID（ABP 默认 `Guid`），需要仓储接口定义于 Domain、实现于 EF Core。
- **namespace 规则（强制执行，扁平根命名空间）**：namespace **一律只写项目名，不再追加子目录层级**——同一项目内所有 `.cs` 共用同一个命名空间，与文件落在哪个子目录无关（`Rag/`、`Documents/`、`HealthChecks/`、`Pages/` 等一律不体现在命名空间里）。对照示例：

  | 文件路径 | namespace |
  |---|---|
  | `src/KnowVault.Ai/Rag/RagService.cs` | `KnowVault.Ai` |
  | `src/KnowVault.Application/Qa/QaAppService.cs` | `KnowVault.Application` |
  | `src/KnowVault.Application.Contracts/Qa/IQaAppService.cs` | `KnowVault.Application.Contracts` |
  | `src/KnowVault.Domain/Chat/Message.cs` | `KnowVault.Domain` |
  | `src/KnowVault.Domain.Shared/Enum/Documents/DocumentStatusEnum.cs` | `KnowVault.Domain.Shared` |
  | `src/KnowVault.Domain.Shared/Localization/KnowVaultResource.cs` | `KnowVault.Domain.Shared` |
  | `src/KnowVault.HttpApi/Controllers/KnowVaultController.cs` | `KnowVault.HttpApi` |
  | `src/KnowVault.HttpApi.Host/HealthChecks/KnowVaultDatabaseCheck.cs` | `KnowVault.HttpApi.Host` |
  | `src/KnowVault.Ingest/Processing/DocumentProcessor.cs` | `KnowVault.Ingest` |
  | `src/KnowVault.DbMigrator/Program.cs` | `KnowVault.DbMigrator` |

  **禁止**省略项目名退化到解决方案级单命名空间（如 `namespace KnowVault;`）——扁平指的是"项目名 + 不追加子目录"，不是所有项目共用一个命名空间。
- **namespace 现存例外（既有现状，不要求专门改动，顺手碰到该文件时再收敛）**：
  - `KnowVault.EntityFrameworkCore`：`.NET/ABP` 约定 `EntityFrameworkCore/` 目录不参与命名空间，故 `src/KnowVault.EntityFrameworkCore/EntityFrameworkCore/KnowVaultDbContext.cs` → `KnowVault.EntityFrameworkCore`；该目录下的 `Configurations/` 目前是 `KnowVault.EntityFrameworkCore.Configurations`（扁平规则的候选收敛对象，非强制）。
  - `src/KnowVault.EntityFrameworkCore/Migrations/*`（EF 自动生成）→ `KnowVault.Migrations`，**属生成物，勿手改命名空间**。
  - `src/KnowVault.Domain/Documents/Document.cs` → 目前仍是 `KnowVault.Domain.Documents`（历史遗留；同目录 `DocumentChunk.cs` 已是 `KnowVault.Domain`）。
- **namespace 一律使用块级（block-scoped）声明**：`.editorconfig` 已强制 `csharp_style_namespace_declarations = block_scoped:error`；禁用 file-scoped（`namespace X;`），否则触发 IDE0160。
- **方法偏好表达式主体（`=> ...`）**：`.editorconfig` 设 `csharp_style_expression_bodied_methods = true:silent`，所以 `public void MarkProcessing() => Status = DocumentStatusEnum.Processing;` 这类写法是**首选风格**，不会再报"使用方法的程序块主体"（IDE0022）。级别 `silent`（VS 里显示为 Refactoring Only）的含义：反向的"改用表达式主体"只以灯泡建议形式出现，**不进错误列表、不参与 Code Cleanup / `dotnet format`**，因此**不强制**改写 ABP 样板里可转的块体方法（实测 18 处，改不改都合规）；多语句方法照旧用 `{ }` 程序块。
- **分支单语句去大括号（强制执行）**：`if` 的**块体只有一条语句**时，**去掉 `{ }`，把该语句和 `if` 写在同一行**（守卫子句 / 提前返回的标准写法）；但同一个 `if / else if / else` 链里只要出现了 `else` 或 `else if`，就**保持大括号，且语句不与条件同行**（各占一行）。
  - 命中（去括号 + 同行）——本次改动样例 `src/KnowVault.EntityFrameworkCore/Storage/MinioDocumentStorage.cs:33`（改前是 `if (_bucketEnsured)` + `{` + `return;` + `}` 四行）：
    ```csharp
    if (_bucketEnsured) return;
    ```
  - 命中（链上有 `else`，保持大括号 + 分行）：
    ```csharp
    if (!exists)
    {
        await _client.MakeBucketAsync(new MakeBucketArgs().WithBucket(_options.BucketName), cancellationToken);
    }
    else
    {
        _logger.LogInformation("bucket {Bucket} 已存在", _options.BucketName);
    }
    ```
  - 判据只看「花括号里有几条语句」：**单条语句但本身跨多行**（方法调用/对象初始化器换行、lambda 体、`await` 链分行）时**仍用大括号并分行**——硬塞成同行会把条件挤到屏幕外，反而更难读。
  - 反向要求：只要是**只有一条语句、且能写成一行**的 `if`，就**不要**写成块体（不要再写 `if (x)` + `{` + `return;` + `}`）。
  - 边界：空体 `if`（`{ }` 或 `;`）保持现状不适用；`for` / `foreach` / `while` / `using` / `try` / `catch` **不在本条规则内**（本仓现状一律带大括号，不强制内联）；`.editorconfig` 未设 `csharp_prefer_braces`，此风格**不参与构建 / `dotnet format`**，靠人工与 review 保证。
- **异步方法一律带 `CancellationToken`（强制执行）**：凡是返回 `Task` / `Task<T>` / `ValueTask` / `ValueTask<T>` / `IAsyncEnumerable<T>` 的方法，签名里**必须有** `CancellationToken`，并且把它**逐层透传**下去——从 API 入口一路传到最底层（EF 仓储、MinIO / Qdrant 客户端、DeepSeek / SiliconFlow 的 HTTP 调用）。
  - **参数形态**：命名固定 `cancellationToken`（不用 `ct` / `token`），位置排在**最后一个参数**（`params` 除外）。
  - **`= default` 的取舍**：控制器 Action / AppService 这类**由 API 入口直接分派**的方法**不加默认值**（ABP 会从当前 HTTP 请求自动注入 `CancellationToken`，强制由入口传入，见 `src/KnowVault.Application/Documents/DocumentAppService.cs:54`）；多层内部的 Domain / Ai / EF 实现（仓储、`IRagService`、`ILlmChatService`、`IDocumentStorage`、`IVectorStore` 等）按仓库既有惯例给 `= default`，便于测试与非 HTTP 场景调用。
  - **必须真的透传**：调用链上任何 `await` 的异步方法都要把本层拿到的 `cancellationToken` 传下去，**不允许吞掉或漏传**——这是客户端断开 / 超时能真正取消的前提，也是"入口传参"这条规范的目的所在。参考已正确的链路：`src/KnowVault.Application/Documents/DocumentAppService.cs:54`（入口）→ `:82` `EnsureBucketAsync(cancellationToken)` / `:88` `PutAsync(…, cancellationToken)` / `:92` 与 `:103`（EF / 发布）；`src/KnowVault.Ai/Rag/RagService.cs:38`（`= default`）→ `:43` `EmbedAsync(…, cancellationToken)`、`:44-48` `SearchAsync(…, cancellationToken)`、`:63` `CompleteAsync(…, cancellationToken)`；ABP 仓储的令牌走**命名参数** `cancellationToken: cancellationToken`（如 `src/KnowVault.Ingest/Processing/DocumentProcessor.cs:59`、`:76`、`:88`），因为前面还有 `autoSave` 等可选参数。
  - **例外（框架决定签名，不要为了本条改动它们）**：ABP / .NET 给出的固定签名——`AbpModule` 生命周期（`OnApplicationInitializationAsync` 等）、`Microsoft.Extensions.Hosting.IHostedService` 的 `StartAsync` / `StopAsync`、`IHealthCheck.CheckHealthAsync`、`DataSeedContributor.SeedAsync(DataSeedContext)`、`KnowVaultDbMigrationService` 这类**迁移 / 种子**方法、MassTransit 的 `IConsumer<T>.Consume(ConsumeContext<T>)`（令牌取自 `context.CancellationToken` 并往下传，见 `src/KnowVault.Ingest/Messaging/DocumentIngestConsumer.cs:47`）、Razor Page 的 `OnGetAsync`、以及 `Main` 入口。这些签名由框架/宿主侧约定，故意不声明 `CancellationToken`。
  - **与「历史文件不动」的关系**：本条**只约束新增的异步方法**；既有签名缺令牌的（实测 3 处：`src/KnowVault.Application/Qa/QaAppService.cs:32` `AskAsync`、`src/KnowVault.Application/KnowledgeBases/KnowledgeBaseAppService.cs:28` `GetListAsync` 与 `:48` `GetDefaultAsync`）**不要为了补规范专门去改**，改这些文件时再顺带收敛（改接口 `IQaAppService` / `IKnowledgeBaseAppService` 必须与实现同步，否则 ABP 动态 API 拿不到令牌）。
- 不写无意义的 `Common`/`Utils` 命名空间；一个 `.cs` 文件对应一个命名空间。

### 前端（Vue 3 / JavaScript）
- **只用 JavaScript，禁止引入 TypeScript**（`.ts` 文件、`tsconfig`、类型注解均不加入）。
- 组件用 **`<script setup>`** 组合式 API 写法；样式尽量 scoped + CSS 变量，参考 Naive UI 主题定制。
- 全局状态用 **Pinia**；Http 请求统一走封装后的 `axios` 实例（统一 baseURL / 拦截器 / 错误处理）。
- 组件/页面文件名用 **PascalCase**（如 `ChatPanel.vue`），composition 文件名用 camelCase。

### 通用
- **注释一律用中文**（项目主语言）；对外文档/README 后续可附英文。
- 提交信息用 **Conventional Commits**（`feat:` / `fix:` / `refactor:` / `docs:` / `chore:` 等）。

### 注释规范（强制执行）
- **原则**：注释用于解释「为什么」而非复述「是什么」；只要是合理的常规代码（如简单 setter 赋值、显而易见的行）不要注释；避免临时调试注释（如 `console.log` 残留）入库。
- **C# 公开/受保护成员（类、方法、属性、枚举值）用 XML 文档注释**：
  - 类/接口：`<summary>` 说明职责（一句话）。
  - 方法：`<summary>` + `<param name="x">…</param>` + `<returns>…</returns>`。
  - **`<returns>` 只写返回值语义**：返回非泛型 `Task`（无返回值的异步方法）时写空的 `<returns></returns>`，**不要写 `<returns>任务</returns>`**；`Task<T>` 则描述 `T` 的含义。
  - 属性/字段/枚举值：`<summary>` 说明含义、取值范围或约束。
  - 构造函数：`<summary>` + 逐个 `<param>`。
  - 常用标签：`<see cref="..."/>`、`<remarks>`、`<c>code</c>`、`<exception cref="...">`。
  - 复杂算法/易误解逻辑：行内 `//` 说明，放在语句上方。
  - **`<summary>` 必须展开为多行，禁止单行**：开标签、正文、闭标签各占一行，正文比开标签再缩进一级（**一个文档注释块内即便只有一句话也照样展开**，不要因为"短"就压回单行）。**规范示例**（`tests/KnowVault.Tests/DocumentChunkerTests.cs:7-9`）：

    ```csharp
    /// <summary>
    /// 短文档只产出一个切片，正文不丢字
    /// </summary>
    [Fact]
    public void Short_document_produces_single_chunk()
    ```

    **反面示例**（标识与内容挤在同一行，勿再用；VS 里敲 `///` 自动补全出来的就是这种单行产物，必须手工展开）：
    ```csharp
    /// <summary>短文档只产出一个切片，正文不丢字。</summary>
    ```
  - **注释正文结尾不写句号**：`<summary>` / `<param>` / `<returns>` / `<exception>` / 类级 `<summary>` 的正文行，以及行内 `//` 说明，**末尾一律不加** `。`（中文）或 `.`（英文点号）。句中标点照常（「，」「、」「；」「：」该用就用），只是不再用句号收尾。**标点收尾的例外**：正文本身以 `？` `！` `；` `：` `）` 等结尾时保持原样，不补句号；**多句正文**句间仍用 `。` 分隔，只去掉整段最后那个句号。**范围例外**：`<remarks>` 里成段的多句说明按普通行文加句号；`<param>` / `<returns>` 内的 `<see cref="…"/>`、`<c>…</c>`、代码示例、命令、URL、文件路径保持原样。
- **C# 自动生成文件**（EF 迁移 `Migrations/*`、`*.Designer.cs`、`AssemblyInfo.cs`）不强制注释，勿随意改动。
- **前端（JS / Vue）**：
  - 具名函数/工具用 JSDoc：`/** ... @param {...} ... @returns {...} */`。
  - 组件（`.vue`）在 `<script setup>` 内：`ref` 状态、具名函数、`onMounted` 逻辑用 `//` 或 JSDoc 说明；模板可用 `<!-- ... -->` 说明复杂块。
  - Pinia store：state 字段与 action 用 JSDoc/`//` 说明。
- **注释与代码同源**：改动代码时同步更新其注释；新增文件按此规范书写。
- **存量文件不做批量回填（历史文件不动）**：上述两条 XML 注释格式（多行 `<summary>`、正文结尾不写句号）**只约束新增代码与本次改动到的注释**；散落在 64 个既有 `.cs` 文件里的约 199 处单行 `<summary>`、约 60 行以句号结尾的 `///` **不要为了对齐格式而专门改动**——纯格式 diff 不带来收益，还会把真实改动淹掉。写新代码、新文件一律按新格式；改既有文件的其它内容时，顺带把该文件**本次触及**的注释收敛到新格式即可（未触及的保持原样）。

### 行尾与换行规范（强制执行）
- **统一使用 CRLF（`\r\n`）**：仓库根 `.editorconfig`（`root = true`）对 `[*]` 声明 `end_of_line = crlf`，`.gitattributes` 声明 `* text=auto eol=crlf`。所有文本文件（`.cs` / `.cshtml` / `.json` / `.js` / `.vue` / `.md` / `.props` / `.slnx` 等）一律 **Windows CRLF**，与 VS2026 类模板、`dotnet new`、`abp new` 在 Windows 上的默认产出保持一致。
  - 实测依据：VS2026 模板 `ItemTemplates\CSharp\...\Class.cs` 是纯 CRLF；`dotnet new` 即使 `.editorconfig` 写 `end_of_line = lf` 也照样产出 CRLF（模板引擎不读该键）。
- **例外（保持 LF）**：`Dockerfile` / `Dockerfile.local` 与 `*.sh`——CRLF 会破坏行继续符，`.gitattributes` 与 `.editorconfig` 已单独钉为 LF。
- **例外（保持 LF）：规范文件自身**——`.editorconfig` 与本文档 `AGENTS.md` 目前都是**纯 LF、无 BOM**，且没有格式化流程会去改写它们；**不要**为了"全仓统一 CRLF"而对这两份文件做行尾归一化（只会产生无意义 diff）。其余新增/改动的文本文件仍按上面的 CRLF 规则执行。
- **禁止单个文件混用行尾**：同一文件同时含 `CRLF` 与 `LF`，是**唯一**会触发 VS2026「不一致的行尾，是否将行尾标准化？」弹窗的条件（整文件统一 CRLF 或统一 LF 都不会弹）。弹窗只是症状，成因永远是"某个写入方只改写了文件的一部分"。
- **不要相信 git**：`.gitattributes` 的 `text` 属性会让 git 在写库时把 CRLF 归一化成 LF，`eol` 只作用于工作区。因此**行尾问题在 `git status` / `git diff` 里完全看不出来**，必须逐字节自查（见下）。
- **已知根因（曾发生，且会复发）**：Windows 侧写入方（VS 模板、`dotnet new`、`abp new`、从聊天窗口/网页粘贴的代码块）默认产出 CRLF，而此前 `.editorconfig` 要求 LF；两套标准相遇时，只要有一次改写是**局部**的（补丁式编辑、局部粘贴、格式化只碰了改动的行），就留下"少数 CRLF 行 + 多数 LF 行"的混合文件。历史指纹：混进去的往往是结构性行（namespace 开括号、方法收尾 `}`）与空行。
- **预防（生成/改动代码后必做）**：
  1. 写入前先看该文件当前的行尾，**整文件保持一致**；改既有文件时先匹配它现有行尾。
  2. 用脚手架/工具生成产物（`abp new`、`dotnet new`、`npm` 等）后，立刻检查产物行尾并与本规范对齐。
  3. 粘贴代码后必须检查——粘贴内容会保留来源的行尾字符。
  4. 提交/交付前逐文件检测：任一文本文件同时含 CRLF 与 LF 即视为不合规，须整文件归一化。
  5. 归一化手段（统一转 CRLF；`Dockerfile*` / `*.sh` 除外）：
     ```powershell
     $p='<文件路径>'; [IO.File]::WriteAllText($p, ([IO.File]::ReadAllText($p) -replace "`r`n","`n") -replace "`n","`r`n")
     ```
     或对整目录批量处理。
  6. 自查命令（判据：两个数字必须有一个为 0）：
     ```powershell
     $p='<文件路径>'; $t=[IO.File]::ReadAllText($p)
     "CRLF=$(([regex]::Matches($t,"`r`n")).Count) loneLF=$(([regex]::Matches($t,"(?<!`r)`n")).Count)"
     ```
- **编码**：`charset = utf-8`（**不带 BOM**）。注意 VS2026 的类模板自带 UTF-8 BOM，新建文件后按本规范去掉 BOM。

---

## 四、数据与 AI 层约定（红线）

- **Qdrant**：**单一 collection（`knowvault_chunks`，1024 维 Cosine）+ payload 过滤**。每个向量的 payload 必须带 `tenantId / knowledgeBaseId / documentId / chunkIndex`，另带 `content / documentTitle / headingPath / sourceType`（见 `TECH_STACK.md` §9.1）；禁止为每个知识库各建一个 collection。
  - ⚠️ 租户为 null 时（单租户运行），payload 的 `tenantId` 必须**归一化为 `Guid.Empty`**——Qdrant 无法对 null 做 match，否则过滤条件恒不命中。
- **切片 Chunking**：按段落/标题层级 + 固定长度；默认 chunk≈500–800 token、overlap≈50–100；参数可配置，并保留段落/标题 metadata 供引用溯源。
  - P1 以**字符数近似 token**（默认 1000 字符 / overlap 120）：中文没有可靠 .NET tokenizer，不为此引入 tiktoken 类依赖；超长单段按句子边界硬切、此时不叠加 overlap。
- **检索 + 组装**：按 `tenantId + knowledgeBaseId` 做 payload 过滤（P2 起改为「宽召回约 20 条 → 重排精排 → 取 top-k≈5–8」）；System prompt 规定「只根据资料回答、资料不足就说不知道、标注来源」；把 top-k 切片 + 问题交给 DeepSeek，返回 `answer + citations`。
- **模型路由**：生成=DeepSeek，向量化/重排=SiliconFlow（`BAAI/bge-m3` + `BAAI/bge-reranker-v2-m3`）。统一封装成 OpenAI 兼容调用层，便于日后换厂商；换 embedding 模型必须重新向量化，只换 rerank 模型不需要。
- **文档解析库**：PDF=`PdfPig`、Word=`DocumentFormat.OpenXml`、Excel=`.xlsx`→**`ClosedXML`**（弃 NPOI）、Markdown=`Markdig`、HTML=`HtmlAgilityPack`。P1 的「网页」指**上传 .html 文件**，不做 URL 抓取。
  - ⚠️ **禁止用 iTextSharp（AGPL 许可，会污染整个项目的开源许可）。**
- 会话/消息：**从第一天就持久化 `Conversation` + `Message`（含 `CitationsJson`）**。

---

## 五、构建 / 运行 / 测试

```bash
# 后端（含 Aspire 一键编排启动 Postgres/Redis/Qdrant/MinIO/RabbitMQ/Seq）
dotnet run --project aspire/KnowVault.AppHost

# 仅后端单个服务
dotnet run --project src/KnowVault.HttpApi.Host

# 前端
cd frontend && npm install && npm run dev

# 测试（含 Testcontainers，需要 Docker 运行）
dotnet test
```

- 依赖用 **Aspire** 启动，避免手工起容器。
- **API Key（DeepSeek/SiliconFlow）放环境变量**，**严禁进仓库/hardcode**（用 `.env` + 环境变量注入；变量清单参照根目录 `.env.example`）。
- 提交/PR 前保证 `dotnet build` 无错误、`dotnet test` 通过。

---

## 六、DSH / Agent 协作约定

- 改动代码前先 `read` 相关文件，遵循既有结构与命名习惯；新增文件遵循第四节约定。
- 遇到「AI 编排 / RAG / 向量检索」类问题，优先看 `src/KnowVault.Ai`，不要散落到其它分层。
- 涉及多租户隔离、Qdrant 过滤、切片策略等**关键设计**改动时，同步更新 `TECH_STACK.md` 与本文档，保持两者一致。
- 这是一个 **AI + 架构双主线**的提升竞争力的个人项目：优先保证**可演示的 RAG 闭环**，其次才是纵深优化。

---

## 七、待办/注意

- DeepSeek / SiliconFlow 的 API Key：**P1 已确认会配置**（端到端真跑）。Key 一律走环境变量 `Ai__DeepSeekApiKey` / `Ai__SiliconFlowApiKey`（由 `aspire/KnowVault.AppHost` 从本地 `.env`/进程环境映射），严禁进仓库或硬编码。
- `src/`、`frontend/` 已落地为 ABP 分层骨架（P0）。**P1** 新增 `tests/KnowVault.Tests`（切片/解析/payload/Prompt 的**纯函数单测**）；**P3** 新增 `tests/KnowVault.IntegrationTests`（Postgres/RabbitMQ/MinIO/Qdrant 容器；本机 MinIO 镜像不可达时可设 `KNOWVAULT_SKIP_MINIO=1` 用内存存储兜底，CI 用真实 MinIO）。
- **P1 冻结规格与执行计划**：`docs/plans/2026-10-05-knowvault-p1-mvp.md`（含 14 个任务的文件级改动、代码与逐项验收命令）。规格变更必须先改该文档的 §0 再改代码。
- **P2 冻结规格与执行计划**：`docs/plans/2026-10-14-knowvault-p2-mvp.md`（含 6 项决策、8 个任务的文件级改动、15 项端到端验收）。规格变更必须先改该文档的 §0 再改代码。P2 起 SignalR 服务端零 NuGet 包（.NET 10 共享框架自带），前端新增 npm `@microsoft/signalr ^10.0.11`；重排配置按 §七「Embedding / Rerank 切换 SiliconFlow」执行（与 Embedding 共用 `Ai:SiliconFlowBaseUrl`）。
- **P2 已完成（2026-10-14）**：15 项端到端验收通过并逐项留证（见该文档 §2 / §5）。两条实测结论后续写 AppService 时会用到：①ABP 动态 API 把 `UpdateAsync(输入DTO)` 映射为 `PUT /api/app/{服务名}`（**路由不带 `{id}`**，实体 ID 走请求体），只有 `DeleteAsync(Guid id)` 才是 `DELETE /api/app/{服务名}/{id}`；②`/api` 前缀的认证失败已改写为 401 + `WWW-Authenticate: Bearer`（其它路径仍跳登录页）。
- **P3 冻结规格与执行计划**：`docs/plans/2026-10-18-knowvault-p3-mvp.md`（含 5 项决策、T0–T7 文件级改动、12 项端到端验收）。P3 新增：Seq sink + OTLP/HTTP protobuf trace（EF 插桩锁 `1.17.0-beta.1`）；系统角色 `Admin/Member/Viewer` + 知识库成员表 `AppKnowledgeBaseMembers`（Owner/Editor/Viewer）；文档删除/重新处理；`document-ingest` 5×2s 重试 + `document-ingest_error`；Testcontainers 四件套；CI 暂未接入（不使用 GitHub，后续考虑 Gitee Go）。**权限缓存**：改角色/成员后需重新登录或等待缓存过期再验收 403。
- **文档去重（2026-10-23）**：同一知识库内按原始文件名（忽略大小写）拒绝重复上传；`Document.SourceFileName` 可空、存量不回填，详见 `TECH_STACK.md` §9.4。
- **Embedding / Rerank 切换 SiliconFlow（2026-10-23）**：向量化 `BAAI/bge-m3`（1024 维）+ 重排 `BAAI/bge-reranker-v2-m3`，共用 `Ai:SiliconFlowBaseUrl=https://api.siliconflow.cn/v1` 与 `Ai:SiliconFlowApiKey`；Qwen/DashScope 代码与配置已移除，历史数据已清空无需迁移。详见 `TECH_STACK.md` §9.5 与 `docs/plans/2026-10-23-siliconflow-model-switch.md`。
