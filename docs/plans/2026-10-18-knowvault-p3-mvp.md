# KnowVault P3「工程深度」实施计划（2026-10-18）

> 执行方式：按 T0 → T7 顺序推进，一个 Task 完成并自测通过后再进入下一个；每个 Task 独立提交，禁止跨任务混改。
> 铁律：不改 P1/P2 已验收行为；不引商业许可包（MassTransit 9.x、FluentAssertions 8.x）；所有新增异步方法带 `CancellationToken` 并逐层透传；新增 `.cs` 用块级 namespace、`Enum` 后缀、CRLF、UTF-8 无 BOM。
> 分支：`feat/p3-mvp`（自 `main` 切出）。
> 关联文档：`TECH_STACK.md` §6 / §9、`docs/plans/2026-10-14-knowvault-p2-mvp.md`。

---

## Goal

把 KnowVault 从「能跑通的 RAG Demo」推到「可观测、可测试、有权限、能进 CI」的工程形态，形成面试可讲的深度：

1. 一条提问在 Seq 里能看到完整 trace（HTTP → ABP → RAG → LLM → EF/SQL）。
2. 文档可以删除、可以重新处理，Qdrant 与 MinIO 不留下明显垃圾。
3. 摄入失败会真的重试，重试耗尽进 error queue，而不是静默失败。
4. 权限从「登录即可」升级为「系统角色 + 知识库成员」双层控制。
5. Testcontainers 起真依赖跑集成测试，GitHub Actions 三 job 全绿。

## Architecture

```text
浏览器 / Swagger
   │  Bearer
   ▼
HttpApi.Host ── OpenTelemetry SDK ── OTLP/HTTP ── Seq（trace + 日志）
   │
   ├─ Application（权限校验 / ACL / 删除编排）
   ├─ Ai（RAG / DeepSeek / Qwen）── ActivitySource: KnowVault.Ai
   ├─ Ingest（MassTransit consumer）── ActivitySource: KnowVault.Ingest
   │      │
   │      └─ RabbitMQ（document-ingest → 5 次重试 → document-ingest_error）
   ├─ EF Core（PostgreSQL）
   ├─ Qdrant（单 collection + payload 过滤）
   └─ MinIO（原文件）
```

## 新增依赖

| 项目 | 包 | 版本 | 用途 |
|---|---|---|---|
| HttpApi.Host | OpenTelemetry.Extensions.Hosting | 1.20.0 | OTel 宿主 |
| HttpApi.Host | OpenTelemetry.Instrumentation.AspNetCore | 1.20.0 | 入站 HTTP span |
| HttpApi.Host | OpenTelemetry.Instrumentation.Http | 1.20.0 | 出站 HTTP span |
| HttpApi.Host | OpenTelemetry.Instrumentation.EntityFrameworkCore | 1.17.0-beta.1 | EF/SQL span |
| HttpApi.Host | OpenTelemetry.Exporter.OpenTelemetryProtocol | 1.20.0 | OTLP 导出 |
| HttpApi.Host | Serilog.Sinks.Seq | 9.1.0 | 日志进 Seq |
| tests/KnowVault.Tests | Testcontainers | 4.16.0 | 容器编排 |
| tests/KnowVault.Tests | Testcontainers.PostgreSql | 4.16.0 | PostgreSQL |
| tests/KnowVault.Tests | Testcontainers.RabbitMq | 4.16.0 | RabbitMQ |
| tests/KnowVault.Tests | Testcontainers.Minio | 4.15.0 | MinIO |
| tests/KnowVault.Tests | Volo.Abp.TestBase | 10.5.0 | ABP 集成测试基类 |
| tests/KnowVault.Tests | Volo.Abp.AspNetCore.TestBase | 10.5.0 | HTTP 冒烟 |
| tests/KnowVault.Tests | Microsoft.AspNetCore.Mvc.Testing | 10.0.13 | WebApplicationFactory |
| tests/KnowVault.Tests | FluentAssertions | 7.2.0 | 断言（**锁 7.x**） |

---

## §0 冻结规格

### 0.1 已确认决策

| 编号 | 决策 | 说明 |
|---|---|---|
| P3-D1 | 范围 = 路线图四项 + 文档删除/重新处理 | 独立 Ingest Worker、MinIO 定时清理不做 |
| P3-D2 | 权限 = 系统角色 + 知识库成员 | Admin/Member/Viewer + Owner/Editor/Viewer |
| P3-D3 | 默认知识库对所有登录用户 Editor | 保 demo 可上传；新建库创建者自动 Owner |
| P3-D4 | Testcontainers 四件套全量 | PostgreSQL + RabbitMQ + MinIO + Qdrant |
| P3-D5 | 可观测只接 Seq | 不接 Aspire Dashboard，避免双出口 |

### 0.2 默认项

| 编号 | 默认 | 理由 |
|---|---|---|
| D1 | 系统角色名 `Admin` / `Member` / `Viewer` | 与 ABP 自带 `admin` 角色并存，管理员用户同时加入 `Admin` |
| D2 | 知识库角色 `Owner=3` / `Editor=2` / `Viewer=1` | 数值越大权限越高，比较用 `>=` |
| D3 | 删除顺序：Qdrant → chunks → document → MinIO | 前两步失败保留文档可重试；MinIO 失败只告警 |
| D4 | 重试 5 次、间隔 2s，error queue `document-ingest_error` | 现有 `UseMessageRetry` 行为，改成可观测、可运营 |
| D5 | 重处理先删旧向量/chunks，再 `MarkPending`，再发消息 | 避免重复 chunk；MinIO 原文件不动 |
| D6 | 权限缓存默认 5 分钟 | ABP 默认；验收时重登或等缓存过期 |
| D7 | Qdrant 建 keyword payload index | `tenantId` / `knowledgeBaseId` / `documentId`，否则过滤删除会全扫 |
| D8 | 测试用假 Embedding（固定 1024 维） | 不依赖外部 Qwen Key，测试可重复 |

### 0.3 风险与已核实结论

| 编号 | 风险/结论 | 处理 |
|---|---|---|
| R1 | `Microsoft.AspNetCore.Mvc.Testing` 直接 `WebApplicationFactory<Program>` 可能起不来（Host 的 `InitializeApplicationAsync` 不在工厂默认流程） | 服务级用 `AbpIntegratedTest<KnowVaultTestModule>`；HTTP 只做 1 个健康检查冒烟，必要时改 `Program` 为 `public partial class Program` |
| R2 | EF OTel 插桩是 `1.17.0-beta.1` | 锁版本；只开默认 span，不开 `db.query.parameter.*` |
| R3 | Qdrant 按 payload 过滤删除，未建索引会全 collection 扫描 | 启动初始化时补 `CreateFieldIndexAsync`，失败只 warning |
| R4 | MassTransit 9.x 商业许可 | 全仓锁 8.5.7，不升级 |
| R5 | FluentAssertions 8.x 商业许可 | 锁 7.2.0 |
| R6 | ABP 权限缓存导致验收时「刚授权仍 403」 | 验收前重登；文档写明 |
| R7 | Testcontainers 需要本机 Docker | CI 用 `ubuntu-latest`；本地先 `docker info` |
| R8 | Seq OTLP 只支持 HTTP/protobuf；gRPC 需 HTTPS | exporter 固定 `OtlpExportProtocol.HttpProtobuf`，端点 `/ingest/otlp/v1/traces` |

### 0.4 非目标

- 不拆独立 Ingest Worker（保留 P4）。
- 不做多租户隔离强化（结构保留，单租户运行）。
- 不做 URL 抓取、OCR、BM25、会话列表/管理 API。
- 不做 MinIO 定时孤儿清理（删除失败只告警，P4 扫描）。
- 不接 Aspire Dashboard / Jaeger / Prometheus。
- 不做前端管理后台（角色管理沿用 ABP 自带 Identity 页面；知识库成员用轻量弹窗）。

### 0.5 测试策略

| 层 | 工具 | 覆盖 |
|---|---|---|
| 纯函数单测 | xUnit（现有） | chunker / parser / payload / prompt / rerank parser |
| 服务级集成 | Testcontainers + `AbpIntegratedTest` | 上传→摄入→Ready、RAG 检索、删除清理、重处理、Viewer 越权 |
| HTTP 冒烟 | `AbpWebApplicationFactoryIntegratedTest` 或 `WebApplicationFactory` | `/health-status` 200、未登录 `/api/app/knowledge-base` 401 |
| CI | GitHub Actions | build、unit、integration 三 job |

---

## §1 任务总览与依赖顺序

| Task | 内容 | 依赖 | 主要产出 |
|---|---|---|---|
| T0 | 基线 + 计划文档 + 分支 | - | 本文档、干净构建 |
| T1 | OpenTelemetry + Seq 全链路 | T0 | trace 可在 Seq 查看 |
| T2 | 系统角色 + 知识库成员 ACL | T1 | 权限表、角色种子、403 |
| T3 | 文档删除 + 重新处理 | T2 | DELETE / reprocess API、清理 |
| T4 | 摄入重试 + error queue | T3 | 真重试、失败可运营 |
| T5 | Testcontainers 集成测试 | T1–T4 | 四件套测试 |
| T6 | GitHub Actions CI | T5 | 三 job 工作流 |
| T7 | 文档 + 端到端验收 | T0–T6 | 验收记录、README/导读 |

每个 Task 收尾统一执行：

```powershell
dotnet build KnowVault.slnx
dotnet test tests/KnowVault.Tests/KnowVault.Tests.csproj
dotnet format style KnowVault.slnx --verify-no-changes --diagnostics IDE1006
git add -A
git commit -m "feat(p3): <task summary>"
```

---

## Task 0：基线、分支与计划

### Files

- 新增：`docs/plans/2026-10-18-knowvault-p3-mvp.md`（本文档）
- 修改：`.env.example`（补 `SEQ_URL`，如无）
- 修改：`TECH_STACK.md`（T7 再更新 P3 状态，T0 不动）

### Steps

1. `git switch -c feat/p3-mvp`（已完成）。
2. `dotnet build KnowVault.slnx` 记录基线；`dotnet test tests/KnowVault.Tests/KnowVault.Tests.csproj` 记录 32/32。
3. 写本文档，提交 `docs(p3): add P3 implementation plan`。

### 验收

- `git status` 干净；`dotnet build` 0 错误；单测全绿。

---

## Task 1：OpenTelemetry + Seq 全链路

### Files

- 修改：`src/KnowVault.HttpApi.Host/KnowVault.HttpApi.Host.csproj`（6 个包）
- 新增：`src/KnowVault.Domain.Shared/Telemetry/KnowVaultTelemetry.cs`
- 新增：`src/KnowVault.HttpApi.Host/Observability/KnowVaultObservabilityExtensions.cs`
- 新增：`src/KnowVault.HttpApi.Host/Observability/ActivityTraceEnricher.cs`
- 修改：`src/KnowVault.HttpApi.Host/Program.cs`
- 修改：`src/KnowVault.HttpApi.Host/KnowVaultHttpApiHostModule.cs`
- 修改：`src/KnowVault.HttpApi.Host/appsettings.json`
- 修改：`aspire/KnowVault.AppHost/Program.cs`
- 修改：`src/KnowVault.Application/Qa/QaStreamService.cs`
- 修改：`src/KnowVault.Ai/Rag/RagService.cs`
- 修改：`src/KnowVault.Ai/Embeddings/QwenEmbeddingService.cs`（如类名不同，按实际）
- 修改：`src/KnowVault.Ai/Chat/DeepSeekChatService.cs`
- 修改：`src/KnowVault.Ai/Rerank/QwenRerankService.cs`
- 修改：`src/KnowVault.Ingest/Processing/DocumentProcessor.cs`

### Step 1.1 包与配置

Host 加包（见「新增依赖」）。`appsettings.json` 增加：

```json
"Observability": {
  "OtlpEndpoint": "http://localhost:5341/ingest/otlp/v1/traces",
  "ServiceName": "KnowVault.HttpApi.Host"
},
"Seq": {
  "ServerUrl": "http://localhost:5341"
}
```

`appsettings.Development.json` 可覆盖为同一值；`Seq:ApiKey` 留空。

### Step 1.2 Telemetry 常量

```csharp
namespace KnowVault.Domain.Shared;

/// <summary>
/// 全链路追踪的统一 ActivitySource 名称
/// </summary>
public static class KnowVaultTelemetry
{
    /// <summary>
    /// 自定义 span 的 ActivitySource 名称
    /// </summary>
    public const string ActivitySourceName = "KnowVault";

    /// <summary>
    /// Seq 中显示的服务名
    /// </summary>
    public const string ServiceName = "KnowVault";
}
```

### Step 1.3 OTel 注册

`KnowVaultObservabilityExtensions.AddKnowVaultObservability`：

- `services.AddOpenTelemetry().ConfigureResource(r => r.AddService(...))`
- `.WithTracing(t => t.AddSource(KnowVaultTelemetry.ActivitySourceName)
    .AddAspNetCoreInstrumentation(o => o.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/health"))
    .AddHttpClientInstrumentation()
    .AddEntityFrameworkCoreInstrumentation())`
- 端点非空时 `.AddOtlpExporter(o => { o.Endpoint = new Uri(endpoint); o.Protocol = OtlpExportProtocol.HttpProtobuf; })`
- 不导 metrics（P3 只做 trace）。

`Program.cs` 在 `UseSerilog` 的 lambda 里追加：

```csharp
var seqUrl = context.Configuration["Seq:ServerUrl"];
if (!string.IsNullOrWhiteSpace(seqUrl))
{
    loggerConfiguration.WriteTo.Seq(seqUrl);
}
loggerConfiguration.Enrich.With<ActivityTraceEnricher>();
```

`KnowVaultHttpApiHostModule.ConfigureServices` 末尾调用 `context.Services.AddKnowVaultObservability(context.Services.GetConfiguration())`。

### Step 1.4 自定义 span

| 位置 | span 名 | 关键 tag |
|---|---|---|
| `QaStreamService.AskStreamAsync` | `qa.ask` | `qa.knowledge_base_id`、`qa.question_length` |
| `RagService.AnswerStreamAsync` / `RetrieveAsync` | `rag.retrieve` | `rag.top_k`、`rag.candidate_count`、`rag.rerank` |
| `QwenEmbeddingService.EmbedAsync` | `embedding.embed` | `embedding.batch_size` |
| `DeepSeekChatService` | `llm.complete` | `llm.model`、`llm.history_count` |
| `QwenRerankService.RerankAsync` | `rerank.rerank` | `rerank.document_count` |
| `DocumentProcessor.ProcessAsync` | `ingest.process` | `document.id`、`document.knowledge_base_id` |

实现约定：`private static readonly ActivitySource ActivitySource = new(KnowVaultTelemetry.ActivitySourceName);`；`using var activity = ActivitySource.StartActivity("xxx");`；异常时 `activity?.SetStatus(ActivityStatusCode.Error, ex.Message)`。

### Step 1.5 Aspire 注入

`aspire/KnowVault.AppHost/Program.cs` 在 `seq` 资源创建后：

```csharp
var seqEndpoint = seq.GetEndpoint("http");
api.WithEnvironment("Seq__ServerUrl", seqEndpoint)
   .WithEnvironment("Observability__OtlpEndpoint",
       ReferenceExpression.Create($"{seqEndpoint}/ingest/otlp/v1/traces"));
```

若 `ReferenceExpression` 在 13.5.3 不可用，退化为 `WithEnvironment("Observability__OtlpEndpoint", seqEndpoint + "/ingest/otlp/v1/traces")`（先确认 `seqEndpoint` 可隐式转 string）。

### 验收

1. `dotnet build` 0 错误。
2. `dotnet run --project aspire/KnowVault.AppHost`，打开 `http://localhost:5341`。
3. Swagger 登录 → 调用 `/api/app/qa` 提问 → Seq 按 `ServiceName = KnowVault` 过滤，能看到一条 trace，至少含：
   - `HTTP POST /api/app/qa`
   - `qa.ask`
   - `rag.retrieve`
   - `llm.complete`
   - EF/SQL span（如 `SELECT ... AppDocuments`）
4. 上传文档 → 摄入 → Seq 能看到 `ingest.process`，父级是 RabbitMQ consume span。

---

## Task 2：系统角色 + 知识库成员 ACL

### Files

- 修改：`src/KnowVault.Application.Contracts/Permissions/KnowVaultPermissions.cs`
- 修改：`src/KnowVault.Application.Contracts/Permissions/KnowVaultPermissionDefinitionProvider.cs`
- 新增：`src/KnowVault.Domain.Shared/Enum/KnowledgeBases/KnowledgeBaseRoleEnum.cs`
- 新增：`src/KnowVault.Domain/Models/KnowledgeBases/KnowledgeBaseMember.cs`
- 修改：`src/KnowVault.Domain/Models/KnowledgeBases/KnowledgeBase.cs`（如需 owner 字段，不加以成员表为准）
- 新增：`src/KnowVault.EntityFrameworkCore/EntityFrameworkCore/Configurations/KnowledgeBaseMemberConfiguration.cs`
- 修改：`src/KnowVault.EntityFrameworkCore/EntityFrameworkCore/KnowVaultDbContext.cs`
- 新增迁移：`P3_KnowledgeBaseMembers`
- 新增：`src/KnowVault.Domain/Models/KnowledgeBases/IKnowledgeBaseMemberRepository.cs`（或直接用 `IRepository<KnowledgeBaseMember, Guid>`）
- 新增：`src/KnowVault.Application/KnowledgeBases/KnowledgeBaseAccessChecker.cs`
- 新增：`src/KnowVault.Application/Data/KnowVaultIdentityDataSeedContributor.cs`
- 修改：`src/KnowVault.Application/KnowledgeBases/KnowledgeBaseAppService.cs`
- 修改：`src/KnowVault.Application/Documents/DocumentAppService.cs`
- 修改：`src/KnowVault.Application/Qa/QaStreamService.cs`
- 修改：`src/KnowVault.Application.Contracts/Interfaces/IKnowledgeBaseAppService.cs`
- 新增：`src/KnowVault.Application.Contracts/Models/KnowledgeBases/KnowledgeBaseMemberDto.cs`
- 新增：`src/KnowVault.Application.Contracts/Models/KnowledgeBases/AddKnowledgeBaseMemberInput.cs`
- 新增：`src/KnowVault.Application.Contracts/Models/KnowledgeBases/UpdateKnowledgeBaseMemberInput.cs`
- 修改：`frontend/src/components/KnowledgeBasePanel.vue`
- 修改：`frontend/src/stores/documents.js`
- 修改：`frontend/src/stores/auth.js`（存角色/权限，可选）

### Step 2.1 权限常量

```csharp
public static class KnowVaultPermissions
{
    public const string GroupName = "KnowVault";

    public static class KnowledgeBases
    {
        public const string Create = GroupName + ".KnowledgeBases.Create";
        public const string Manage = GroupName + ".KnowledgeBases.Manage";
        public const string ManageMembers = GroupName + ".KnowledgeBases.ManageMembers";
    }

    public static class Documents
    {
        public const string Upload = GroupName + ".Documents.Upload";
        public const string Delete = GroupName + ".Documents.Delete";
        public const string Reprocess = GroupName + ".Documents.Reprocess";
    }

    public static class Qa
    {
        public const string Ask = GroupName + ".Qa.Ask";
    }
}
```

Provider 里逐项 `group.AddPermission(..., "中文显示名")`，不用子权限（减少 UI 层级）。

### Step 2.2 角色与授权

| 角色 | 权限 |
|---|---|
| `Admin` | 全部（含 `KnowledgeBases.Manage`） |
| `Member` | `KnowledgeBases.Create`、`Documents.Upload/Delete/Reprocess`、`Qa.Ask` |
| `Viewer` | `Qa.Ask` |

种子逻辑：确保三角色存在；`admin` 用户加入 `Admin`；用 `IPermissionManager.SetForRoleAsync(role, permission, true)` 授权。已存在则幂等跳过。

### Step 2.3 知识库成员实体

```csharp
public class KnowledgeBaseMember : CreationAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid KnowledgeBaseId { get; private set; }
    public Guid UserId { get; private set; }
    public KnowledgeBaseRoleEnum Role { get; private set; }
    public Guid? TenantId { get; private set; }

    protected KnowledgeBaseMember() { }

    public KnowledgeBaseMember(Guid id, Guid knowledgeBaseId, Guid userId,
        KnowledgeBaseRoleEnum role, Guid? tenantId = null) : base(id)
    {
        KnowledgeBaseId = knowledgeBaseId;
        UserId = userId;
        Role = role;
        TenantId = tenantId;
    }

    public void ChangeRole(KnowledgeBaseRoleEnum role) => Role = role;
}
```

EF 配置：表 `AppKnowledgeBaseMembers`，`ConfigureByConvention()`，唯一索引 `(KnowledgeBaseId, UserId)`，`Role` 存 int，`UserId` 普通索引。

### Step 2.4 访问检查器

`KnowledgeBaseAccessChecker`（`ITransientDependency`）：

```csharp
Task<KnowledgeBaseRoleEnum> GetRoleAsync(Guid knowledgeBaseId, CancellationToken ct = default);
Task EnsureAsync(Guid knowledgeBaseId, KnowledgeBaseRoleEnum required, CancellationToken ct = default);
Task<IReadOnlyList<Guid>> GetAccessibleIdsAsync(CancellationToken ct = default);
```

规则：

1. 默认知识库 → 所有登录用户按 `Editor` 处理。
2. 当前用户是 `Admin`（`IPermissionChecker.IsGrantedAsync(KnowVaultPermissions.KnowledgeBases.Manage)`）→ `Owner`。
3. 查 `KnowledgeBaseMember`（`CurrentUser.Id`）→ 返回角色；无记录 → 抛 `AbpAuthorizationException`（403）。
4. `EnsureAsync` 用 `role >= required` 比较。

`IPermissionChecker` 在 `Volo.Abp.Authorization.Permissions`；`AbpAuthorizationException` 在 `Volo.Abp.Authorization`。

### Step 2.5 AppService 接入

| 方法 | 校验 |
|---|---|
| `KnowledgeBaseAppService.GetListAsync` | 只返回默认库 + 当前用户是成员的库 |
| `GetDefaultAsync` | 默认库直接返回 |
| `CreateAsync` | 要求 `KnowledgeBases.Create`；插入 KB 后插入 `Owner` 成员 |
| `UpdateAsync` | `EnsureAsync(Owner)` |
| `DeleteAsync` | `EnsureAsync(Owner)`；仍拒绝非空库 |
| `DocumentAppService.GetAsync/GetListAsync` | `EnsureAsync(Viewer)` |
| `UploadAsync` | `EnsureAsync(Editor)` + `[Authorize(Documents.Upload)]`（控制器已有 `[Authorize]`，方法级加权限） |
| `DeleteAsync/ReprocessAsync` | `EnsureAsync(Editor)` + 对应权限 |
| `QaStreamService.AskStreamAsync` | `EnsureAsync(Viewer)` |

权限特性只加在 AppService 方法或控制器；`QaHub` 已有 `[Authorize]`，`QaStreamService` 内部做 ACL。

### Step 2.6 成员 API

`IKnowledgeBaseAppService` 增加：

```csharp
Task<List<KnowledgeBaseMemberDto>> GetMembersAsync(Guid knowledgeBaseId, CancellationToken ct);
Task AddMemberAsync(AddKnowledgeBaseMemberInput input, CancellationToken ct);
Task UpdateMemberAsync(UpdateKnowledgeBaseMemberInput input, CancellationToken ct);
Task RemoveMemberAsync(Guid knowledgeBaseId, Guid userId, CancellationToken ct);
```

DTO：`KnowledgeBaseMemberDto { Guid UserId; string UserName; string? Email; KnowledgeBaseRoleEnum Role; }`。用户名从 `IIdentityUserRepository` 批量取；找不到显示 userId。

ABP 动态 API 路由：`GET /api/app/knowledge-base/{knowledgeBaseId}/members` 这类自定义方法可能映射成 `GET /api/app/knowledge-base/members?knowledgeBaseId=`；以实际 Swagger 为准，前端用实际路由。

### Step 2.7 前端

- `KnowledgeBasePanel.vue` 每行加「成员」按钮（仅 Owner/Admin 可见；简化：所有登录用户显示，后端 403 时提示）。
- 弹窗：成员列表 + 添加（输入用户邮箱或用户名）+ 角色下拉 + 删除。
- 由于没有用户搜索 API，先用「输入用户 Id」或「邮箱」；若实现成本高，P3 只做后端 ACL + 前端只读成员列表，添加成员用 Swagger。**默认按「后端完整 + 前端只读列表 + Swagger 添加」交付，避免前端造用户选择器。**

### 验收

1. 用 `admin` 登录，创建知识库 → `AppKnowledgeBaseMembers` 有 Owner 记录。
2. 创建 `viewer` 用户并加入该库 Viewer；viewer 登录后能看库/文档/提问，上传返回 403。
3. viewer 的 `GET /api/app/knowledge-base` 不返回未加入的库。
4. admin 可删除空库；Owner 可改库名；Editor 不能改库名。
5. `dotnet format style --verify-no-changes --diagnostics IDE1006` 通过。

---

## Task 3：文档删除 + 重新处理

### Files

- 修改：`src/KnowVault.Domain/Models/Documents/Document.cs`（`MarkPending`、`IngestAttemptCount`）
- 修改：`src/KnowVault.Domain/VectorStore/IVectorStore.cs`（`DeleteByDocumentAsync`）
- 修改：`src/KnowVault.EntityFrameworkCore/VectorStore/QdrantVectorStore.cs`
- 修改：`src/KnowVault.Domain/Storage/IDocumentStorage.cs`（`RemoveAsync`）
- 修改：`src/KnowVault.EntityFrameworkCore/Storage/MinioDocumentStorage.cs`
- 修改：`src/KnowVault.Application.Contracts/Interfaces/IDocumentAppService.cs`
- 修改：`src/KnowVault.Application/Documents/DocumentAppService.cs`
- 修改：`src/KnowVault.Application.Contracts/Models/Documents/DocumentDto.cs`（`IngestAttemptCount`）
- 修改：`src/KnowVault.HttpApi/Controllers/DocumentUploadController.cs`（DELETE / reprocess）
- 修改：`frontend/src/components/DocumentPanel.vue`
- 修改：`frontend/src/stores/documents.js`

### Step 3.1 接口

```csharp
// IVectorStore
Task DeleteByDocumentAsync(Guid tenantId, Guid knowledgeBaseId, Guid documentId,
    CancellationToken cancellationToken = default);

// IDocumentStorage
Task RemoveAsync(string objectKey, CancellationToken cancellationToken = default);
```

Qdrant 实现：

```csharp
var filter = new Filter
{
    Must =
    {
        new Condition { Field = new FieldCondition { Key = ChunkPayload.TenantId,
            Match = new Match { Keyword = tenantId.ToString() } } },
        new Condition { Field = new FieldCondition { Key = ChunkPayload.KnowledgeBaseId,
            Match = new Match { Keyword = knowledgeBaseId.ToString() } } },
        new Condition { Field = new FieldCondition { Key = ChunkPayload.DocumentId,
            Match = new Match { Keyword = documentId.ToString() } } }
    }
};
await _client.DeleteAsync(_options.CollectionName, filter, wait: true, cancellationToken);
```

MinIO 实现：`_client.RemoveObjectAsync(new RemoveObjectArgs().WithBucket(_options.BucketName).WithObject(objectKey), cancellationToken)`。

### Step 3.2 删除编排

`DocumentAppService.DeleteAsync(Guid id, CancellationToken)`：

1. `GetAsync` + `EnsureAsync(Editor)` + `[Authorize(Documents.Delete)]`。
2. `DeleteByDocumentAsync(tenantId, kbId, id)`。
3. `_chunkRepository.DeleteAsync(x => x.DocumentId == id, autoSave: true)`。
4. `_documentRepository.DeleteAsync(document, autoSave: true)`。
5. try `_storage.RemoveAsync(objectKey)` catch 记 warning，不阻断。
6. 返回 `Task`。

顺序必须在文档删除前完成向量/chunk；第 2/3 步失败抛异常，文档保留，用户可重试删除。

### Step 3.3 重处理

`ReprocessAsync(Guid id, CancellationToken)`：

1. `EnsureAsync(Editor)` + `[Authorize(Documents.Reprocess)]`。
2. `DeleteByDocumentAsync`。
3. `_chunkRepository.DeleteAsync(x => x.DocumentId == id, autoSave: true)`。
4. `document.MarkPending(); await _documentRepository.UpdateAsync(document, autoSave: true);`
5. `unitOfWork.OnCompleted(... PublishAsync(document.Id, document.TenantId, ct))`；无 UoW 直接 publish。

`MarkPending` 清空 `ErrorMessage`；`IngestAttemptCount` 在 processor 每次开始时 `++`，便于前端展示“第 N 次”。

### Step 3.4 API

`DocumentUploadController`：

```csharp
[HttpDelete("{id:guid}")]
public Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    => _documentAppService.DeleteAsync(id, cancellationToken);

[HttpPost("{id:guid}/reprocess")]
public Task ReprocessAsync(Guid id, CancellationToken cancellationToken)
    => _documentAppService.ReprocessAsync(id, cancellationToken);
```

### Step 3.5 前端

`DocumentPanel.vue` 每行加「重新处理」「删除」按钮；删除 popconfirm；成功后 `store.refresh()`。`documents.js` 增加 `removeDocument(id)`、`reprocessDocument(id)`。

### 验收

1. 上传 `.md` → Ready；删除 → 文档列表消失，Qdrant `GET /collections/knowvault_chunks/points/scroll` 按 documentId 过滤为 0，MinIO 对象 404。
2. 重新处理 Ready 文档 → 状态回到 Pending→Ready，`AppDocumentChunks` 数量不翻倍。
3. 删除非空知识库仍被拒绝（提示先删文档）。
4. 删除/重处理 Viewer 返回 403。

---

## Task 4：摄入重试 + error queue

### Files

- 修改：`src/KnowVault.Ingest/Processing/DocumentProcessor.cs`
- 修改：`src/KnowVault.Ingest/KnowVaultIngestModule.cs`
- 修改：`src/KnowVault.Ingest/Messaging/DocumentIngestConsumer.cs`
- 修改：`src/KnowVault.Application.Contracts/Models/Documents/DocumentDto.cs`（重试次数）
- 修改：`frontend/src/components/DocumentPanel.vue`（失败显示次数，可选）

### Step 4.1 Processor 真重试

把 catch 改为：

```csharp
catch (Exception exception) when (exception is not OperationCanceledException)
{
    Logger.LogError(exception, "文档 {DocumentId} 摄入失败（第 {Attempt} 次）", documentId, document.IngestAttemptCount);
    document.MarkFailed(exception.Message);
    await _documentRepository.UpdateAsync(document, autoSave: true, cancellationToken: cancellationToken);
    throw;
}
```

`ProcessInternalAsync` 开头 `document.IngestAttemptCount++;` 并 Update。取消异常不标记失败、直接抛，让 MassTransit 停止。

### Step 4.2 端点与 error queue

`KnowVaultIngestModule.UsingRabbitMq` 内改为显式端点：

```csharp
rabbit.ReceiveEndpoint("document-ingest", endpoint =>
{
    endpoint.UseMessageRetry(retry => retry.Interval(5, TimeSpan.FromSeconds(2)));
    endpoint.ConfigureConsumer<DocumentIngestConsumer>(busContext);
});
```

删除 `rabbit.ConfigureEndpoints(busContext)`（避免重复端点）。MassTransit 8 默认 `_error` 后缀，端点名 `document-ingest` → `document-ingest_error`。

### Step 4.3 Consumer 日志

`Consume` 里 `Logger.LogInformation("开始处理文档 {DocumentId}（租户 {TenantId}）", ...)`；异常不 catch，让重试生效。

### 验收

1. 上传一个损坏/无法解析的文件（或把 MinIO 对象删掉再重处理）→ 失败 5 次后文档 `Failed`，`ErrorMessage` 有值，`IngestAttemptCount=5`。
2. RabbitMQ 管理台（`http://localhost:15672`）看到 `document-ingest_error` 有 1 条消息。
3. 修复后点「重新处理」→ 成功 Ready，attempt 继续累加（不复位，作为历史）。
4. Seq 能看到每次 `ingest.process` 失败 span 与错误日志。

---

## Task 5：Testcontainers 集成测试

### Files

- 修改：`tests/KnowVault.Tests/KnowVault.Tests.csproj`
- 新增：`tests/KnowVault.Tests/Integration/KnowVaultContainerFixture.cs`
- 新增：`tests/KnowVault.Tests/Integration/KnowVaultTestModule.cs`
- 新增：`tests/KnowVault.Tests/Integration/KnowVaultIntegratedTestBase.cs`
- 新增：`tests/KnowVault.Tests/Integration/FakeEmbeddingService.cs`
- 新增：`tests/KnowVault.Tests/Integration/DocumentLifecycleTests.cs`
- 新增：`tests/KnowVault.Tests/Integration/PermissionTests.cs`
- 新增：`tests/KnowVault.Tests/Integration/HealthEndpointTests.cs`
- 新增：`tests/KnowVault.Tests/Integration/TestWait.cs`

### Step 5.1 容器 Fixture

`KnowVaultContainerFixture : IAsyncLifetime`：

- `PostgreSqlBuilder().WithImage("postgres:16-alpine").Build()`
- `RabbitMqBuilder().WithImage("rabbitmq:3-management").Build()`
- `MinioBuilder().WithImage("minio/minio:latest").Build()`
- `QdrantBuilder`：Testcontainers 没有官方 Qdrant 模块，用 `new ContainerBuilder().WithImage("qdrant/qdrant:v1.19.0").WithPortBinding(6334, true).WithPortBinding(6333, true).WithWaitStrategy(Wait.ForHttp("/").ForPort(6333))`。
- `StartAsync` 全部启动；`DisposeAsync` 停止。
- 暴露 `ConnectionString`、`RabbitMqConnectionString`、`MinioEndpoint/AccessKey/SecretKey`、`QdrantEndpoint`。

### Step 5.2 测试模块

`KnowVaultTestModule` DependsOn：`AbpAutofacModule`、`AbpTestBaseModule`、`KnowVaultDomainModule`、`KnowVaultEntityFrameworkCoreModule`、`KnowVaultApplicationModule`、`KnowVaultIngestModule`。

`ConfigureServices`：

- 用 `context.Services.ReplaceConfiguration` 或 `Configure<AbpDbConnectionOptions>` 把 `Default` 指向容器。
- `Configure<QdrantOptions>`、`Configure<MinioOptions>`、`Configure<AiOptions>`（假 Key）。
- `context.Services.Replace(ServiceDescriptor.Singleton<IEmbeddingService, FakeEmbeddingService>())`。
- `context.Services.AddAlwaysAllowAuthorization()`（HTTP 冒烟除外）。

`OnApplicationInitializationAsync`：`await context.ServiceProvider.GetRequiredService<IKnowVaultDbSchemaMigrator>().MigrateAsync();` 然后 `IDataSeeder.SeedAsync()`。

### Step 5.3 基类

```csharp
public abstract class KnowVaultIntegratedTestBase : AbpIntegratedTest<KnowVaultTestModule>
{
    protected override void SetAbpApplicationCreationOptions(AbpApplicationCreationOptions options) => options.UseAutofac();
    protected override void BeforeAddApplication(IServiceCollection services) => services.AddAlwaysAllowAuthorization();
}
```

Fixture 用 `ICollectionFixture<KnowVaultContainerFixture>` 共享容器；连接串通过静态 `TestEnvironment` 在 fixture 启动后赋值，测试模块读取。

### Step 5.4 用例

| 用例 | 断言 |
|---|---|
| `Upload_Should_Ingest_To_Ready` | 上传 `.md` → 轮询 30s 内 `Ready`；`DocumentChunks` > 0；Qdrant 点数 > 0 |
| `Reprocess_Should_Not_Duplicate_Chunks` | 重处理前后 chunk 数一致 |
| `Delete_Should_Clean_Vector_And_Minio` | 删除后 Qdrant 点 0、MinIO `StatObject` 抛 not found、文档查不到 |
| `Viewer_Upload_Should_403` | 切换到 viewer 身份，调用上传 → `AbpAuthorizationException` |
| `Health_Should_Return_200` | HTTP 冒烟（如工厂可用） |

轮询用 `TestWait.UntilAsync(Func<Task<bool>>, timeout)`，禁止 `Task.Delay` 硬等。

### 验收

- `dotnet test` 在本机 Docker 运行下全绿；失败信息能定位。
- 不设置真实 AI Key 也能跑（假 Embedding；RAG 检索用例只测 `IVectorStore.SearchAsync`，不测 DeepSeek）。

---

## Task 6：GitHub Actions CI

### Files

- 新增：`.github/workflows/ci.yml`
- 新增：`.github/workflows/ci-frontend.yml`（可合并）

### Step 6.1 三 job

```yaml
name: ci
on:
  push:
    branches: [main, feat/**]
  pull_request:
jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with: { dotnet-version: '10.0.x' }
      - run: dotnet restore KnowVault.slnx
      - run: dotnet build KnowVault.slnx --no-restore -warnaserror
      - run: dotnet format style KnowVault.slnx --verify-no-changes --diagnostics IDE1006
  unit:
    needs: build
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with: { dotnet-version: '10.0.x' }
      - run: dotnet test tests/KnowVault.Tests/KnowVault.Tests.csproj --filter "FullyQualifiedName!~Integration"
  integration:
    needs: build
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with: { dotnet-version: '10.0.x' }
      - run: dotnet test tests/KnowVault.Tests/KnowVault.Tests.csproj --filter "FullyQualifiedName~Integration"
  frontend:
    needs: build
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with: { node-version: '22' }
      - run: cd frontend && npm ci && npm run build
```

Testcontainers 在 `ubuntu-latest` 直接用 Docker；不需要 `services:`。

### 验收

- 推送分支后 GitHub Actions 四个 job 绿；`dotnet format` job 能拦住无 `Enum` 后缀的枚举。

---

## Task 7：文档与端到端验收

### Files

- 修改：`TECH_STACK.md`（P3 状态、P4 待办）
- 修改：`AGENTS.md`（权限、删除、测试、CI 约定）
- 新增：`docs/P3-代码导读.md`
- 修改：`docs/plans/2026-10-18-knowvault-p3-mvp.md`（§5 执行记录）

### 验收

逐项填写 §2；全部通过后提交 `docs(p3): record acceptance`。

---

## §2 端到端验收清单

| # | 验收项 | 命令/操作 | 期望 |
|---|---|---|---|
| 1 | 构建 | `dotnet build KnowVault.slnx` | 0 error |
| 2 | 单测 | `dotnet test --filter "FullyQualifiedName!~Integration"` | 全绿 |
| 3 | 集成测试 | `dotnet test --filter "FullyQualifiedName~Integration"` | 全绿 |
| 4 | 代码风格 | `dotnet format style ... --diagnostics IDE1006` | 无变更 |
| 5 | Seq trace | 提问一次 | HTTP→qa.ask→rag.retrieve→llm.complete + EF span |
| 6 | 摄入 trace | 上传一次 | ingest.process + RabbitMQ consume 父 span |
| 7 | 权限 | viewer 上传 | 403 |
| 8 | 删除 | 删除文档 | Qdrant 0 点、MinIO 404 |
| 9 | 重处理 | 重处理 Ready 文档 | chunk 数不翻倍 |
| 10 | 重试 | 造失败 | 5 次后 Failed + error queue 1 条 |
| 11 | CI | push 分支 | 4 job 绿 |
| 12 | 前端 | `npm run build` | 成功 |

## §3 风险登记

| 风险 | 概率 | 影响 | 缓解 |
|---|---|---|---|
| `AbpIntegratedTest` 启动慢/模块依赖踩坑 | 高 | 中 | 先做最小模块（Domain+EF+Application），Ingest 后加；失败降级为「容器级测试 + 手工验收」并在文档标注 |
| Qdrant 容器健康检查不稳定 | 中 | 中 | 用 HTTP 6333 `/` 等待；重试 3 次 |
| 权限缓存导致测试偶发 403 | 中 | 低 | 测试里 `AddAlwaysAllowAuthorization` 或直接 mock `IPermissionChecker` |
| EF OTel beta 升级破坏 | 低 | 低 | 锁 1.17.0-beta.1 |
| MinIO 删除对象不存在抛异常 | 高 | 低 | catch 并 warning |
| 前端成员管理做不完 | 中 | 低 | 后端完整 + 前端只读列表 + Swagger 添加 |

## §4 完成定义（DoD）

- [ ] T0–T7 全部完成，每任务独立 commit。
- [ ] `dotnet build` 0 error；单测 + 集成测试全绿。
- [ ] Seq 可见完整 trace；`ingest.process` 可见。
- [ ] 权限矩阵 5 条验收通过；删除/重处理/重试 3 条验收通过。
- [ ] GitHub Actions 四 job 绿。
- [ ] `TECH_STACK.md` / `AGENTS.md` / `docs/P3-代码导读.md` 与实现一致。
- [ ] 无商业许可包；无硬编码 Key；无 `as any` 类临时绕过。

## §5 执行记录

| 日期 | Task | 结果 | 证据 |
|---|---|---|---|
| 2026-10-18 | T0 | 分支 `feat/p3-mvp` 创建；计划文档落地 | 本文件 |
