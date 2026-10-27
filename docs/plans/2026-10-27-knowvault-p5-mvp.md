# KnowVault P5「检索含金量」实施计划（2026-10-27）

> 状态：**已确认（2026-10-27），T0 开工**
> 执行方式：按 T0 → T9 顺序推进；每个 Task 完成并自测通过后再进入下一个，独立提交，禁止跨任务混改
> 铁律：不改 P1–P4 已验收行为；所有新增异步方法带 `CancellationToken` 并逐层透传；新增 `.cs` 用块级 namespace、`Enum` 后缀、CRLF、UTF-8 无 BOM；新增/触及的注释按 `AGENTS.md` 的 XML 规范
> 前置文档：`docs/driver/需求/需求分析-1027-P5.md`（需求权威）、`docs/plans/2026-10-24-knowvault-open-source-roadmap.md`（范围权威）、`TECH_STACK.md`（技术选型权威）
> 分支建议：`feat/p5-retrieval-quality`（自 `main` 切出）
> 本文是 P5 的冻结规格：范围、契约或默认值变更必须先改本文的 §0，再改代码

---

## Goal

把检索从「单路 dense 召回」升级为**可配置、可度量、可解释**的混合检索，并补齐问答范围与成本视图：

1. 关键词稀疏召回 + 向量召回，应用层加权 RRF 融合，参数可配置
2. LLM 多查询改写，可开关，失败不影响问答
3. 多库 / 全局问答，逐库权限校验，会话 scope 可恢复
4. 30 题金标集 + 三组对比 + 指标报告入仓
5. 生成 / 向量化 / 重排 / 改写的 token、耗时与费用统计，仪表盘按天、按库展示

对应路线图 P5-T0–T5。需求细节见 `docs/driver/需求/需求分析-1027-P5.md`，本文只展开文件级改动与验收方式。

## Architecture

```text
提问
  │
  ├─ 范围解析（KnowledgeBaseScopeResolver）
  │    单库 / 多库 / 全部可访问库 → 逐库 Viewer 校验 → scope 写回会话
  │
  ├─ 查询改写（可选，QueryRewriteService）
  │    原问题 + 最多 2 条改写；失败 fail-open
  │
  ├─ 批量 Embedding（SiliconFlow bge-m3）
  │
  ├─ 每查询两路召回（Qdrant knowvault_chunks）
  │    dense（unnamed，1024 维 Cosine）
  │    sparse（named "text"，Modifier.Idf）
  │
  ├─ 应用层加权 RRF（ReciprocalRankFusion）
  │    Σ 查询 Σ 分支 权重 × 1/(RrfK + rank)
  │
  ├─ 重排（SiliconFlow bge-reranker-v2-m3，用原问题）
  │
  └─ 取 topK → 引用（带知识库 ID / 名称）→ DeepSeek 流式生成

旁路：每次模型调用 → IAiUsageRecorder（独立 UoW，不阻断主流程）
      → AppAiUsageRecords → DashboardAppService → 概览页用量区

评估：tools/KnowVault.Eval → 直接复用 RagService / QdrantVectorStore
      → 三组模式（vector / hybrid / hybrid-rewrite）→ docs/eval/reports/*.md + *.json
```

---

## §0 冻结规格

### 0.1 已确认决策

以下沿用需求文档 §14 的推荐项；确认时若调整，先改本节与需求文档，再动代码。

| 编号 | 决策 | 取值 |
|---|---|---|
| D1 | 混合检索方案 | Qdrant 原生稀疏向量（IDF modifier）+ 应用层加权 RRF；不引入 OpenSearch / PostgreSQL 全文 |
| D2 | 中文关键词切分 | CJK 重叠二元组 + 最小停用词；纯 C#，不引 Jieba / tiktoken |
| D3 | 默认开关与 UI | 混合默认开、改写默认关；问答页头部「检索设置」两个开关，localStorage 持久化 |
| D4 | 多库交互与成本归属 | 多选 + 「全部知识库」；会话按主库归组；多库用量单列「多库/全局」 |
| D5 | 评估语料 | 项目自写 6 篇 RAG 主题 Markdown + 30 题金标集 |
| D6 | 索引升级 | Admin 维护接口重建 collection + 批量重处理；不自动迁移 |

### 0.2 默认参数

| 配置键 | 默认值 | 范围/约束 | 说明 |
|---|---|---|---|
| `Ai:HybridEnabled` | `true` | bool | 混合检索总开关，请求可覆盖 |
| `Ai:Hybrid:DenseWeight` | `1.0` | 0–3 | dense 分支权重 |
| `Ai:Hybrid:KeywordWeight` | `1.0` | 0–3 | 稀疏分支权重 |
| `Ai:Hybrid:VariantWeight` | `0.8` | 0–3 | 改写查询权重（原问题固定 1.0） |
| `Ai:Hybrid:RrfK` | `60` | 10–100 | RRF 平滑常数 |
| `Ai:Hybrid:PerQueryCandidateCount` | `10` | 5–30 | 每查询每分支召回数 |
| `Ai:RerankEnabled` | `true` | bool | 沿用 |
| `Ai:RerankCandidateCount` | `20` | 1–100 | 融合后进入重排的候选上限，沿用 |
| `Ai:TopK` | `5` | 1–8 | 最终引用条数，沿用 |
| `Ai:QueryRewriteEnabled` | `false` | bool | 改写总开关，请求可覆盖 |
| `Ai:QueryRewrite:MaxVariants` | `2` | 0–2 | 最多改写条数；0 视为关闭 |
| `Ai:QueryRewrite:MinQuestionLength` | `6` | ≥0 | 过短问题不改写 |
| `Ai:QueryRewrite:MaxTokens` | `200` | 50–500 | 改写调用输出上限 |
| `Ai:HistoryMaxMessages` | `6` | 沿用 | 问答历史；改写只取最近 2 条 |
| `Ai:Pricing:DeepSeekInputPerMillion` | `2` | CNY / 百万 token | T5 开工当日核对官网后确认 |
| `Ai:Pricing:DeepSeekOutputPerMillion` | `8` | CNY / 百万 token | 同上 |
| `Ai:Pricing:EmbeddingPerMillion` | `0.5` | CNY / 百万 token | 同上 |
| `Ai:Pricing:RerankPerMillion` | `0.5` | CNY / 百万 token | 同上 |

补充规则：

- `topK` 上限仍为 8；单库时取 `input.TopK ?? 知识库.TopK ?? 0`，多库/全部时取 `input.TopK ?? 0`，最终由 `RagService` clamp。
- 多库提问不按库配额；共用一个候选池、一次重排。
- 多库提问不使用单库自定义系统提示词；单库提问沿用现有逻辑。
- `KeywordWeight=0` 时结果应与纯 dense 的 topK 一致；`DenseWeight=0` 时只返回关键词命中。
- `Citation.Score`：重排开启为重排分，关闭为 RRF 分；不跨问题比较，前端保留三位小数。

### 0.3 契约与数据模型变更

#### 0.3.1 API / DTO

| 位置 | 变更 |
|---|---|
| `AskQuestionInput` | **删除** `KnowledgeBaseId`；新增 `List<Guid>? KnowledgeBaseIds`、`bool AllKnowledgeBases`、`bool? HybridEnabled`、`bool? RewriteEnabled` |
| `ConversationDto` | 新增 `IReadOnlyList<Guid> KnowledgeBaseIds`、`bool AllKnowledgeBases` |
| `RagCitationDto` | 新增 `Guid KnowledgeBaseId`、`string? KnowledgeBaseName` |
| `QaStreamEventDto` | `Started` 帧新增 `KnowledgeBaseIds`、`HybridEnabled`、`RewriteQueries` |
| `POST /api/app/maintenance/rebuild-vector-index` | 新增，Admin-only，返回 `RebuildVectorIndexResultDto` |
| `GET /api/app/dashboard` | `DashboardDto` 新增 `Usage` 子对象 |

`Completed` 帧**不新增** token / 成本字段；本次用量统一在仪表盘查看，避免事件契约再扩一层。

#### 0.3.2 数据库

| 表 | 变更 | 迁移 |
|---|---|---|
| `AppConversations` | 新增 `KnowledgeBaseScopeJson`（text, null）、`ScopeAll`（bool, default false） | `P5_ConversationScope` |
| `AppAiUsageRecords` | 新表：用量记录 | `P5_AiUsageRecords` |

`AppAiUsageRecords` 字段：

| 字段 | 类型 | 说明 |
|---|---|---|
| `Id` | Guid | 主键 |
| `TenantId` | Guid? | 多租户预留 |
| `KnowledgeBaseId` | Guid? | 多库/全局为 null |
| `ConversationId` | Guid? | 问答类有值 |
| `AskId` | Guid? | 同一次问答关联；摄取为 null |
| `DocumentId` | Guid? | 摄取 embedding 有值 |
| `Operation` | `AiOperationEnum` | Chat / Embedding / Rerank / QueryRewrite |
| `Model` | string(128) | 实际模型名 |
| `PromptTokens` | int | 输入 token |
| `CompletionTokens` | int | 输出 token；embedding/rerank 为 0 |
| `TotalTokens` | int | 汇总 |
| `LatencyMs` | int | 该操作耗时 |
| `IsEstimated` | bool | usage 缺失时为 true |
| `EstimatedCostCny` | decimal(18,6) | 写入时按当时价格计算 |
| `CreationTime` / `CreatorId` | 继承 | 问答记录按当前用户过滤 |

索引：`(CreationTime)`、`(KnowledgeBaseId, CreationTime)`、`(AskId)`。

#### 0.3.3 Qdrant

- 单 collection `knowvault_chunks` 不变。
- **unnamed dense**：1024 维 Cosine，保持不变。
- **named sparse `text`**：`SparseVectorParams { Modifier = Modifier.Idf }`。
- 稀疏索引文本 = `HeadingPath + "\n" + Content`；payload 的 `content` 不变。
- 切词：NFKC + 小写；CJK 重叠二元组（单字保留）；拉丁/数字连续片段长度 ≥ 2；最小停用词；FNV-1a 32 位哈希（禁止 `string.GetHashCode()`）。
- 文档侧权重 `1 + ln(tf)`；查询侧每个唯一词元 1.0。
- 旧 collection 缺少 `text` 时 `EnsureCollectionAsync` 抛 `VectorSchemaMismatchException`，提示执行重建接口；不静默降级。

#### 0.3.4 配置

`src/KnowVault.HttpApi.Host/appsettings.json` 的 `Ai` 段新增：

```json
{
  "HybridEnabled": true,
  "Hybrid": {
    "DenseWeight": 1.0,
    "KeywordWeight": 1.0,
    "VariantWeight": 0.8,
    "RrfK": 60,
    "PerQueryCandidateCount": 10
  },
  "QueryRewriteEnabled": false,
  "QueryRewrite": {
    "MaxVariants": 2,
    "MinQuestionLength": 6,
    "MaxTokens": 200
  },
  "Pricing": {
    "Currency": "CNY",
    "DeepSeekInputPerMillion": 2,
    "DeepSeekOutputPerMillion": 8,
    "EmbeddingPerMillion": 0.5,
    "RerankPerMillion": 0.5
  }
}
```

全部支持环境变量覆盖（`Ai__Hybrid__KeywordWeight` 等），AppHost 不需要新增映射。

### 0.4 非目标

- 不做 SPLADE / 学习型稀疏、同义词词典、查询分类/路由
- 不做 OpenSearch / Elasticsearch / PostgreSQL 全文 / zhparser
- 不做评估 UI、LLM-as-judge、自动调参
- 不做按知识库配置混合权重或改写开关（只做全局配置 + 请求级开关）
- 不做多库按库配额、按库轮流召回
- 不做 Docker Compose、README、示例数据正式交付（P6）
- 不做 `Completed` 帧 token / 成本展示
- 不做 embedding 缓存命中价、历史用量回填、旧引用库名回填
- 不改会话侧栏整体交互，只增加多库标识与 scope 过滤规则

### 0.5 测试策略

- 单测：切词、稀疏权重、RRF、改写解析、token 估算、费用计算、仪表盘聚合、排序指标、引用解析
- RagService 单测：混合开关、权重、稀疏失败降级、改写失败降级、纯向量快速路径、引用分语义
- 集成测试（Testcontainers）：dense + sparse 写入后关键词命中、多库过滤、摄取 embedding 用量落库、现有文档生命周期回归
- 评估：30 题 × 三组；报告入仓；`--retrieval-only` 可快速回归
- 手工验收：需求 §11 清单逐项走查；每次提交前 `dotnet build` 0 警告、`dotnet test` 全绿、CRLF/BOM 自查

---

## §1 任务总览与依赖顺序

| Task | 内容 | 对应需求编号 | 依赖 | 迁移 |
|---|---|---|---|---|
| T0 | 选型冻结、写回 `TECH_STACK.md` | P5-T0 | — | 无 |
| T1 | 关键词切词、稀疏索引、Qdrant schema、重建接口 | P5-T1 | T0 | 无 |
| T2 | 混合检索、加权 RRF、配置、降级 | P5-T1 | T1 | 无 |
| T3 | 查询改写、非流式 LLM 契约、流式 usage | P5-T2 | T2 | 无 |
| T4 | 多库/全局问答后端 + scope + 前端选择 | P5-T3 | T2 | `P5_ConversationScope` |
| T5 | 用量记录表、记录器、AI 契约 usage | P5-T5 | T1/T2/T3 | `P5_AiUsageRecords` |
| T6 | 仪表盘用量后端 + 前端 | P5-T5 | T5 | 无 |
| T7 | 评估语料、30 题金标、runner、报告 | P5-T4 | T2/T3/T5 | 无 |
| T8 | 前端检索设置、改写展示、引用跳转收尾 | P5-T2/T3 | T4/T6 | 无 |
| T9 | 全量验收、文档同步、质量门禁 | P5-T6 | 全部 | 无 |

建议提交顺序：T0 → T1 → T2 → T3 → T4 → T5 → T6 → T7 → T8 → T9。

### 迁移命令

```bash
dotnet tool install --global dotnet-ef # 首次

dotnet ef migrations add P5_ConversationScope \
  --project src/KnowVault.EntityFrameworkCore \
  --startup-project src/KnowVault.HttpApi.Host \
  --output-dir Migrations

dotnet ef migrations add P5_AiUsageRecords \
  --project src/KnowVault.EntityFrameworkCore \
  --startup-project src/KnowVault.HttpApi.Host \
  --output-dir Migrations

dotnet run --project src/KnowVault.DbMigrator
```

---

## Task 0：选型冻结与文档

**目标**：把 P5-T0 的结论写进技术权威文档，后续任务只按这份规格执行。

### Files

- 修改：`TECH_STACK.md`
- 修改：`docs/driver/需求/需求分析-1027-P5.md`（状态从「待确认」改为「已确认」，仅改状态行）
- 修改：`docs/plans/2026-10-24-knowvault-open-source-roadmap.md`（P5 标记「进行中」，T9 再改「已完成」）

### Steps

1. `TECH_STACK.md` 新增 §9.7「P5 混合检索与用量」：
   - 9.7.1 检索流程：改写 → 批量 embedding → dense + sparse → 加权 RRF → 重排 → topK
   - 9.7.2 中文切词与稀疏向量（bigram、FNV-1a、`1 + ln(tf)`）
   - 9.7.3 Qdrant schema 与重建方式（named sparse `text` + IDF；旧 collection 必须重建）
   - 9.7.4 多库过滤与会话 scope
   - 9.7.5 用量与成本口径（AskId、IsEstimated、价格配置化）
   - 9.7.6 评估方法与三组模式
2. §6 路线图表把 P5 状态改为「进行中」，并链接本文与需求文档。
3. 不改代码。

### 验收

- `TECH_STACK.md` 中能查到本文 §0 的默认值、Qdrant schema 与重建步骤。
- 文档行尾仍为 LF（`TECH_STACK.md` 既有行尾保持一致，不做全文件归一化；新增段落随文件原行尾）。

---

## Task 1：关键词索引与稀疏向量

**目标**：文档摄入时同时写入 dense 与 sparse；旧 collection 能被识别并给出明确重建提示；Admin 可一键重建并重处理。

### Files

新增：

- `src/KnowVault.Domain/Search/KeywordStopWords.cs`
- `src/KnowVault.Domain/Search/KeywordTokenizer.cs`
- `src/KnowVault.Domain/Search/SparseVectorData.cs`
- `src/KnowVault.Domain/Search/SparseVectorBuilder.cs`
- `src/KnowVault.Domain/VectorStore/VectorSchema.cs`
- `src/KnowVault.Domain/VectorStore/VectorSchemaMismatchException.cs`
- `src/KnowVault.Application.Contracts/Interfaces/IMaintenanceAppService.cs`
- `src/KnowVault.Application.Contracts/Models/Maintenance/RebuildVectorIndexResultDto.cs`
- `src/KnowVault.Application/Maintenance/MaintenanceAppService.cs`
- `tests/KnowVault.Tests/KeywordTokenizerTests.cs`
- `tests/KnowVault.Tests/SparseVectorBuilderTests.cs`

修改：

- `src/KnowVault.Domain/VectorStore/VectorModels.cs`
- `src/KnowVault.Domain/VectorStore/IVectorStore.cs`
- `src/KnowVault.EntityFrameworkCore/VectorStore/QdrantVectorStore.cs`
- `src/KnowVault.Ingest/Processing/DocumentProcessor.cs`
- `src/KnowVault.Ingest/Processing/QdrantCollectionInitializer.cs`
- `tests/KnowVault.IntegrationTests/DocumentLifecycleTests.cs`

### Contracts

```csharp
// KeywordTokenizer：纯函数，输入任意文本，输出按出现顺序排列的词元
public static class KeywordTokenizer
{
    public static IReadOnlyList<string> Tokenize(string? text);
}

// 稀疏向量：索引与权重一一对应
public sealed class SparseVectorData
{
    public required uint[] Indices { get; init; }
    public required float[] Values { get; init; }
    public bool IsEmpty => Indices.Length == 0;
}

// 文档侧 1 + ln(tf)，查询侧唯一词元 1.0
public static class SparseVectorBuilder
{
    public static SparseVectorData? BuildDocument(string? text);
    public static SparseVectorData? BuildQuery(string? text);
    public static uint HashToken(string token);
}

// 单 collection 内的向量名与 schema 常量
public static class VectorSchema
{
    public const string SparseVectorName = "text";
}

// VectorPoint 增加稀疏向量；为空表示该切片没有可索引词元
public SparseVectorData? SparseVector { get; set; }
```

```csharp
// IVectorStore 新增（T1 只用 SearchSparseAsync / RecreateCollectionAsync）
Task<IReadOnlyList<VectorSearchHit>> SearchSparseAsync(
    SparseVectorData sparseVector,
    VectorFilter filter,
    int topK,
    CancellationToken cancellationToken = default);

Task RecreateCollectionAsync(CancellationToken cancellationToken = default);
```

```csharp
// Admin 维护接口
public interface IMaintenanceAppService : IApplicationService
{
    Task<RebuildVectorIndexResultDto> RebuildVectorIndexAsync(CancellationToken cancellationToken);
}

public class RebuildVectorIndexResultDto
{
    public int RequeuedCount { get; set; }
    public int SkippedCount { get; set; }
    public IReadOnlyList<Guid> SkippedDocumentIds { get; set; } = [];
}
```

### Steps

1. 切词：
   - 先 `Normalize(NormalizationForm.FormKC)` 再 `ToLowerInvariant()`。
   - CJK 连续片段生成重叠二元组；长度 1 保留单字；不生成单字 + 双字混合组合。
   - 拉丁/数字连续片段保留长度 ≥ 2；不做词干化。
   - 停用词只收需求 §4.2.1 列出的最小集合；命中后丢弃。
   - 哈希用 FNV-1a 32 位，映射到 `1 .. uint.MaxValue - 1`；同一词元跨进程稳定。
2. 稀疏构建：
   - 文档侧统计词频，权重 `1 + Math.Log(tf)`；查询侧唯一词元权重 1.0。
   - 索引按 `uint` 升序输出，便于测试与排查。
   - 无词元返回 null，不写空稀疏向量。
3. Qdrant schema：
   - 创建 collection 时同时传 `sparseVectorsConfig`：`VectorSchema.SparseVectorName` → `SparseVectorParams { Modifier = Modifier.Idf }`。
   - 已存在时读取 collection info，检查 sparse vectors 是否包含 `text`；缺失抛 `VectorSchemaMismatchException`，消息包含 `POST /api/app/maintenance/rebuild-vector-index`。
   - 启动初始化器记录该异常时给出重建提示，仍不阻断启动（保持 P1 行为）。
4. 写入：
   - `UpsertAsync` 用命名向量字典同时写 `""`（dense）与 `text`（sparse）；无稀疏向量的点只写 `""`。
   - `DocumentProcessor` 为每个切片构建稀疏向量：索引文本 = `HeadingPath + "\n" + Content`（标题为空时只用 Content）。
5. 检索：
   - `QdrantVectorStore.SearchSparseAsync` 用 sparse query + 同一过滤条件；payload 回传方式与 dense 一致。
   - 先写一个最小 spike（10 行以内临时测试或单元测试）确认 Qdrant.Client 1.19 的 `Query` / `Vectors` 稀疏写法与元组顺序；确认后删除临时文件。
6. 重建：
   - `RecreateCollectionAsync`：删除 collection → 按新 schema 创建 → 补 payload 索引 → 置 `_collectionEnsured = true`。
   - `MaintenanceAppService`：`[Authorize(KnowVaultPermissions.KnowledgeBases.Manage)]`；先重建 collection；查询 `Ready` 与 `Processing` 文档；`Ready` 逐条调用 `IDocumentAppService.ReprocessAsync`（复用清切片 → MarkPending → 提交后发布），`Processing` 跳过并返回 ID。
   - 路由由 ABP 约定生成：`POST /api/app/maintenance/rebuild-vector-index`；用 Swagger 核对。
7. 集成测试：
   - `DocumentLifecycleTests` 处理后用 `SearchSparseAsync(SparseVectorBuilder.BuildQuery("KV-2026"), filter, 10)` 断言命中；原 dense 断言保留。

### 验收

- `dotnet test tests/KnowVault.Tests` 覆盖：中英混合、全角标点、停用词、单字、哈希稳定、tf 权重、查询权重、空文本。
- 本机旧 collection 启动/首次写入时抛出含重建接口地址的明确错误；执行重建后写入与关键词检索成功。
- 重建接口返回 `RequeuedCount` 与 `SkippedCount`；重建后所有 Ready 文档可检索。
- 现有 `DocumentLifecycleTests` 除新增断言外全部通过。

---

## Task 2：混合检索与加权 RRF

**目标**：同一问题同时走 dense 与 sparse，按应用层加权 RRF 融合后重排；纯向量快速路径与 P4 完全一致。

### Files

新增：

- `src/KnowVault.Ai/Configuration/HybridOptions.cs`
- `src/KnowVault.Ai/Retrieval/RrfModels.cs`
- `src/KnowVault.Ai/Retrieval/ReciprocalRankFusion.cs`
- `tests/KnowVault.Tests/RrfFusionTests.cs`
- `tests/KnowVault.Tests/RagServiceHybridTests.cs`

修改：

- `src/KnowVault.Ai/Configuration/AiOptions.cs`
- `src/KnowVault.Ai/Rag/IRagService.cs`
- `src/KnowVault.Ai/Rag/RagModels.cs`
- `src/KnowVault.Ai/Rag/RagService.cs`
- `src/KnowVault.Domain/VectorStore/VectorModels.cs`
- `src/KnowVault.EntityFrameworkCore/VectorStore/QdrantVectorStore.cs`
- `src/KnowVault.Application.Contracts/Models/Qa/AskQuestionInput.cs`
- `src/KnowVault.Application/Qa/QaStreamService.cs`
- `src/KnowVault.HttpApi.Host/appsettings.json`
- `tests/KnowVault.Tests/RagServiceRerankTests.cs`
- `tests/KnowVault.IntegrationTests/DocumentLifecycleTests.cs`

### Contracts

```csharp
// AiOptions 新增
public bool HybridEnabled { get; set; } = true;
public HybridOptions Hybrid { get; set; } = new();

public class HybridOptions
{
    public double DenseWeight { get; set; } = 1.0;
    public double KeywordWeight { get; set; } = 1.0;
    public double VariantWeight { get; set; } = 0.8;
    public int RrfK { get; set; } = 60;
    public int PerQueryCandidateCount { get; set; } = 10;
}
```

```csharp
// 一条召回分支：ids 已按相关性降序
public sealed class RrfBranch
{
    public required IReadOnlyList<Guid> Ids { get; init; }
    public double Weight { get; init; } = 1.0;
    public double QueryWeight { get; init; } = 1.0;
}

public sealed class RrfCandidate
{
    public Guid Id { get; init; }
    public double Score { get; init; }
    public int BestRank { get; init; }
}

public static class ReciprocalRankFusion
{
    public static IReadOnlyList<RrfCandidate> Fuse(IReadOnlyList<RrfBranch> branches, int rrfK);
}
```

```csharp
// 过滤条件改为集合；单个库仍走 MatchKeyword，多个库走 MatchAny
public class VectorFilter
{
    public Guid TenantId { get; set; }
    public IReadOnlyList<Guid> KnowledgeBaseIds { get; set; } = [];
}
```

```csharp
// RagRequest 新增请求级覆盖；KnowledgeBaseIds 在 T4 改为集合，T2 先保持单库
public bool? HybridEnabled { get; set; }

// 暴露检索结果给单测与 T7 评估工具
public interface IRagService
{
    Task<RagAnswer> AnswerAsync(RagRequest request, CancellationToken cancellationToken = default);
    Task<RagStreamResult> AnswerStreamAsync(RagRequest request, CancellationToken cancellationToken = default);
    Task<RagRetrievalResult> RetrieveAsync(RagRequest request, CancellationToken cancellationToken = default);
}

public class RagRetrievalResult
{
    public required IReadOnlyList<RagRetrievedChunk> Chunks { get; init; }
    public required IReadOnlyList<string> RewriteQueries { get; init; }
}
```

### Steps

1. 配置：
   - `AiOptions` 增加 `HybridEnabled` 与 `Hybrid`；`appsettings.json` 按 §0.3.4 补齐。
   - 取值时 clamp：权重 0–3，`RrfK` 10–100，`PerQueryCandidateCount` 5–30。
2. RRF 纯函数：
   - `score = Σ 分支 (BranchWeight × QueryWeight × 1/(RrfK + rank))`，rank 从 1 开始。
   - 同一文档多路命中累加；同一分支内重复 ID 只取首次排名。
   - 排序：`Score` 降序 → `BestRank` 升序 → `Id` 升序（GUID 作为稳定兜底，等价于文档/切片顺序的确定性）。
3. `VectorFilter` 改集合：
   - `KnowledgeBaseIds.Count == 0` 直接返回空，不查 Qdrant。
   - 1 个 ID 用 `MatchKeyword`；多个用 `MatchAny`（编译时确认 Qdrant.Client 1.19 的条件构造器方法名）。
   - dense / sparse 共用同一个过滤构造方法。
4. `RagService`：
   - 快速路径：`hybridEnabled == false` 且无改写查询时，完全沿用 P4：`candidateCount = RerankEnabled ? clamp(RerankCandidateCount, topK, 100) : topK`，只 embed 原问题、一次 dense 检索。
   - 混合路径：
     1. 原问题 embed；混合开启时对每条查询构建 sparse 查询。
     2. 每条查询：dense limit = `PerQueryCandidateCount`；混合开启时 sparse limit 相同。
     3. 分支权重：dense = `DenseWeight`，sparse = `KeywordWeight`；原问题 `QueryWeight=1.0`，改写查询 = `VariantWeight`。
     4. 按 chunkId 去重，取 `candidateCount`（重排开启为 `RerankCandidateCount`，否则 topK）进入重排。
     5. 重排仍用原问题；失败退回融合顺序。
   - 降级矩阵：
     - sparse 抛错（非取消）→ 记 warning，继续只用 dense。
     - 问题切不出词元 → 跳过 sparse 分支。
     - dense 抛错 → 原样抛出（与 P4 一致）。
     - 重排失败 → 退回融合顺序（沿用现有逻辑）。
   - 引用 `Score`：重排开启为重排分，关闭为 RRF 分。
   - OTel 标签保持 `rag.knowledge_base_id`（多库时用逗号拼接）与 `rag.hit_count`。
5. `QaStreamService`：
   - 传 `HybridEnabled = input.HybridEnabled` 到 `RagRequest`；不改生成与落库逻辑。
6. 测试：
   - 更新 `RagServiceRerankTests` 的 fake 向量库（新过滤集合、`SearchSparseAsync`）。
   - 新增 `RagServiceHybridTests`：混合顺序、权重、`KeywordWeight=0` 等价纯 dense、`DenseWeight=0` 只关键词、稀疏失败降级、纯向量快速路径的候选数。
   - 新增 `RrfFusionTests`：单/多分支、权重、去重累加、同分排序、空输入。

### 验收

- 混合关闭时原有 7 个重排测试全部通过，候选数与顺序不变。
- 混合开启时关键词类查询（如 `KV-2026`）能召回 dense 召不回的内容。
- 稀疏检索故障问答仍成功；日志有 warning。
- `dotnet test tests/KnowVault.Tests` 通过。

---

## Task 3：查询改写

**目标**：可开关的多查询改写；原问题始终保留，最多 2 条变体；任何失败都不影响问答。

### Files

新增：

- `src/KnowVault.Ai/Chat/LlmCompletionResult.cs`
- `src/KnowVault.Ai/Chat/LlmStreamChunk.cs`
- `src/KnowVault.Ai/Chat/LlmUsage.cs`
- `src/KnowVault.Ai/Configuration/QueryRewriteOptions.cs`
- `src/KnowVault.Ai/Rewrite/IQueryRewriteService.cs`
- `src/KnowVault.Ai/Rewrite/QueryRewriteService.cs`
- `src/KnowVault.Ai/Rewrite/QueryRewriteParser.cs`
- `src/KnowVault.Ai/Rewrite/QueryRewritePromptBuilder.cs`
- `src/KnowVault.Ai/Tokens/TokenEstimator.cs`
- `tests/KnowVault.Tests/QueryRewriteParserTests.cs`
- `tests/KnowVault.Tests/TokenEstimatorTests.cs`

修改：

- `src/KnowVault.Ai/Chat/ILlmChatService.cs`
- `src/KnowVault.Ai/Chat/DeepSeekChatService.cs`
- `src/KnowVault.Ai/Configuration/AiOptions.cs`
- `src/KnowVault.Ai/Rag/RagModels.cs`
- `src/KnowVault.Ai/Rag/RagService.cs`
- `src/KnowVault.Ai/Rag/RagStreamResult.cs`
- `src/KnowVault.Application.Contracts/Models/Qa/AskQuestionInput.cs`
- `src/KnowVault.Application.Contracts/Models/Qa/QaStreamEventDto.cs`
- `src/KnowVault.Application/Qa/QaStreamService.cs`
- `src/KnowVault.HttpApi.Host/appsettings.json`
- `tests/KnowVault.Tests/RagServiceRerankTests.cs`
- `tests/KnowVault.Tests/RagServiceHybridTests.cs`

### Contracts

```csharp
public interface ILlmChatService
{
    // 流式：内容与 usage 分开；usage 可能只在最后一帧出现
    IAsyncEnumerable<LlmStreamChunk> CompleteStreamAsync(
        string systemPrompt,
        IReadOnlyList<LlmChatMessage> messages,
        CancellationToken cancellationToken = default);

    // 非流式：改写等短调用
    Task<LlmCompletionResult> CompleteAsync(
        string systemPrompt,
        IReadOnlyList<LlmChatMessage> messages,
        int maxTokens,
        double temperature,
        CancellationToken cancellationToken = default);
}

public class LlmStreamChunk
{
    public string? Delta { get; init; }
    public LlmUsage? Usage { get; init; }
}

public class LlmCompletionResult
{
    public required string Content { get; init; }
    public required string Model { get; init; }
    public LlmUsage? Usage { get; init; }
}

public class LlmUsage
{
    public int PromptTokens { get; init; }
    public int CompletionTokens { get; init; }
    public bool IsEstimated { get; init; }
}
```

```csharp
public interface IQueryRewriteService
{
    Task<QueryRewriteResult> RewriteAsync(
        string question,
        IReadOnlyList<LlmChatMessage> history,
        CancellationToken cancellationToken = default);
}

public class QueryRewriteResult
{
    public required IReadOnlyList<string> Queries { get; init; }
    public required string Model { get; init; }
    public LlmUsage? Usage { get; init; }
}
```

```csharp
// AI 配置新增
public bool QueryRewriteEnabled { get; set; }
public QueryRewriteOptions QueryRewrite { get; set; } = new();

public class QueryRewriteOptions
{
    public int MaxVariants { get; set; } = 2;
    public int MinQuestionLength { get; set; } = 6;
    public int MaxTokens { get; set; } = 200;
}
```

```csharp
// RagRequest 新增
public bool? RewriteEnabled { get; set; }

// RagStreamResult 新增
public required IReadOnlyList<string> RewriteQueries { get; init; }

// Started 事件新增
public bool HybridEnabled { get; set; }
public IReadOnlyList<string> RewriteQueries { get; set; } = [];
```

### Steps

1. SK 契约：
   - 流式改为 `IAsyncEnumerable<LlmStreamChunk>`；空内容帧仍丢弃，但只带 usage 的帧必须保留。
   - `CompleteAsync` 用 `OpenAIPromptExecutionSettings { Temperature, MaxTokens }`。
   - **先做 0.5 小时 spike**：确认 SK 1.80 + DeepSeek 是否透出流式 usage（`StreamOptions.IncludeUsage` / `Metadata["Usage"]` 的实际写法）。能拿到就标记 `IsEstimated=false`；拿不到就由 RagService 按字符估算并标记 true。spike 结论写进 T3 提交说明。
2. Token 估算：
   - `TokenEstimator.Estimate(text)`：CJK 字符 1 字≈1 token，其它非空白字符 4 字符≈1 token，向上取整。
   - 用于 embedding / rerank / 改写 / 流式生成缺失 usage 时的兜底。
3. 改写 Prompt：
   - System：生成用于检索的替代表述，覆盖同义词、缩写、英文术语；不要回答问题；只输出 JSON 字符串数组。
   - 历史只取最近 2 条，先剥掉 `[数字]` 引用标记。
   - `temperature=0`，`max_tokens=QueryRewrite.MaxTokens`。
4. 解析：
   - 允许 ```json 代码块包裹；必须是字符串数组。
   - 去空白、去重（忽略大小写）、丢弃与原文相同项；单条长度 2–120；最多保留 `MaxVariants`。
   - 非法 JSON / 非数组 / 空结果 → 返回空列表，不抛异常。
5. 触发与降级：
   - `RewriteEnabled` 请求级优先，否则全局；问题长度 < `MinQuestionLength` 或 `MaxVariants=0` 时不调用。
   - 调用的任何异常（超时、HTTP、解析）→ 记 warning，只用原问题。
   - 原问题始终排第一，变体不参与最终重排（重排仍用原问题）。
   - 所有查询一次性批量 embedding，减少 HTTP 往返。
6. 事件：
   - `Started` 返回有效 `HybridEnabled` 与 `RewriteQueries`（只有变体，不含原问题）。
   - `QaStreamResult.Deltas` 改为 `IAsyncEnumerable<LlmStreamChunk>`；`QaStreamService` 只把 `Delta` 推给前端。
7. 测试：
   - `QueryRewriteParserTests`：代码块、非法 JSON、非数组、重复、超长、与原文相同、超过上限。
   - `TokenEstimatorTests`：中英混合、空白、空串。
   - RagService：改写开启时 embed 批次包含原问题 + 变体；改写抛错时问答继续且只 embed 原问题。

### 验收

- 改写关闭：无 `QueryRewrite` 调用，`RewriteQueries` 为空。
- 改写开启：最多 2 条变体、原问题第一；`Started` 可见。
- 断网 / 非 JSON / 超时：问答成功，前端无报错，日志 warning。
- 流式生成在有/无 usage 两种情况下都能正常结束；无 usage 时本轮记录标记估算（T5 落库）。

---

## Task 4：多库 / 全局问答

**目标**：一次提问可选多个知识库或全部可访问库；逐库校验；会话 scope 持久化；引用带库名。

### Files

新增：

- `src/KnowVault.Application/KnowledgeBases/KnowledgeBaseScopeResolver.cs`
- `tests/KnowVault.Tests/ConversationScopeTests.cs`
- `tests/KnowVault.IntegrationTests/MultiKnowledgeBaseSearchTests.cs`

修改：

- `src/KnowVault.Application.Contracts/Models/Qa/AskQuestionInput.cs`
- `src/KnowVault.Application.Contracts/Models/Qa/QaStreamEventDto.cs`
- `src/KnowVault.Application.Contracts/Models/Qa/RagCitationDto.cs`
- `src/KnowVault.Application.Contracts/Models/Chat/ConversationDto.cs`
- `src/KnowVault.Application.Contracts/Interfaces/IConversationAppService.cs`（仅注释路由不变）
- `src/KnowVault.Domain/Models/Chat/Conversation.cs`
- `src/KnowVault.Domain/Models/Chat/Conversation.cs` 对应 EF 配置 `src/KnowVault.EntityFrameworkCore/EntityFrameworkCore/Configurations/ConversationConfiguration.cs`
- `src/KnowVault.Application/Chat/ConversationAppService.cs`
- `src/KnowVault.Ai/Rag/RagModels.cs`
- `src/KnowVault.Ai/Rag/RagService.cs`
- `src/KnowVault.Application/Qa/QaStreamService.cs`
- `frontend/src/stores/chat.js`
- `frontend/src/components/ChatPanel.vue`
- `frontend/src/components/ConversationSidebar.vue`
- `frontend/src/components/CitationDrawer.vue`
- `tests/KnowVault.Tests/RagServiceRerankTests.cs`
- `tests/KnowVault.Tests/RagServiceHybridTests.cs`
- 迁移：`Migrations/*P5_ConversationScope*`

### Contracts

```csharp
public class AskQuestionInput
{
    public List<Guid>? KnowledgeBaseIds { get; set; }
    public bool AllKnowledgeBases { get; set; }
    public Guid? ConversationId { get; set; }
    public int? TopK { get; set; }
    public bool Regenerate { get; set; }
    public bool? HybridEnabled { get; set; }
    public bool? RewriteEnabled { get; set; }

    [Required]
    [StringLength(2000)]
    public string Question { get; set; } = string.Empty;
}
```

```csharp
public sealed class KnowledgeBaseScope
{
    public required IReadOnlyList<Guid> Ids { get; init; }
    public bool All { get; init; }
    public required Guid PrimaryId { get; init; }
    public required IReadOnlyDictionary<Guid, string> Names { get; init; }
}

public class KnowledgeBaseScopeResolver : ITransientDependency
{
    public Task<KnowledgeBaseScope> ResolveAsync(
        IReadOnlyCollection<Guid>? requestedIds,
        bool all,
        CancellationToken cancellationToken = default);
}
```

```csharp
// Conversation 新增
public string? KnowledgeBaseScopeJson { get; set; }
public bool ScopeAll { get; set; }

public void UpdateScope(Guid primaryKnowledgeBaseId, IReadOnlyList<Guid> knowledgeBaseIds, bool all);

// ConversationDto 新增
public IReadOnlyList<Guid> KnowledgeBaseIds { get; set; } = [];
public bool AllKnowledgeBases { get; set; }

// RagCitation 新增
public Guid KnowledgeBaseId { get; set; }

// RagCitationDto 新增
public Guid KnowledgeBaseId { get; set; }
public string? KnowledgeBaseName { get; set; }
```

### Steps

1. 范围解析：
   - `AllKnowledgeBases=true` 且 `KnowledgeBaseIds` 非空 → `UserFriendlyException`。
   - 两者都为空 → `[DefaultKnowledgeBaseId]`。
   - 指定 ID：去重、去空 GUID、保留顺序；逐个 `GetRoleAsync` 做 Viewer 校验；收集无权/不存在的 ID，加载可获取的名称后抛 `UserFriendlyException("无权访问以下知识库：…")`，不静默丢弃。
   - 全部：`GetAccessibleIdsAsync()` 解析为本次实际集合；主库取默认库（若可访问），否则第一个；名称批量加载。
   - 解析结果为空 → `UserFriendlyException("没有可提问的知识库")`。
2. `QaStreamService`：
   - 先解析 scope，再走原会话流程。
   - 单库时读取该库的 `TopK` / `SystemPrompt`；多库/全部时 TopK 用请求值或全局，系统提示词用内置模板。
   - 新建会话：`KnowledgeBaseId = scope.PrimaryId`，随后 `UpdateScope`。
   - 已有会话：按本次解析结果 `UpdateScope` 并保存；`Touch` 仍在完成时执行。
   - `RagRequest` 传 `KnowledgeBaseIds = scope.Ids`。
   - 引用映射时按 `scope.Names` 填 `KnowledgeBaseName`；旧会话引用没有库名时留 null。
   - `Started` 帧带 `KnowledgeBaseIds`（实际解析后的集合）与 `HybridEnabled`。
   - 生成 `AskId`（T5 使用）放到本轮开头生成一次，保证各阶段记录可关联。
3. 会话 DTO：
   - `KnowledgeBaseIds` 从 `KnowledgeBaseScopeJson` 反序列化；为空时回落到 `KnowledgeBaseId` 单值；JSON 非法按空列表处理。
   - `AllKnowledgeBases = ScopeAll`。
4. 检索：
   - `RagRequest.KnowledgeBaseId` 改为 `IReadOnlyList<Guid> KnowledgeBaseIds`；dense/sparse 共用。
   - `RagRetrievedChunk` 增加 `KnowledgeBaseId`（读 payload）。
   - `RagCitation` 增加 `KnowledgeBaseId`。
5. 前端：
   - `chat` store 新增 `scopeIds`、`scopeAll`；`ask`/`regenerate` 改收 scope 对象；payload 发送 `knowledgeBaseIds` / `allKnowledgeBases`。
   - `loadConversation(id, summary)`：从会话摘要恢复 scope；摘要里包含已删除/无权库时，按当前 `documents.knowledgeBases` 过滤并提示，不修改历史消息。
   - `ChatPanel`：「提问范围」改为多选 + 「全部知识库」选项；选择「全部」清空其它项，选择具体库取消「全部」；scope 不写 `documents.knowledgeBaseId`。
   - 引用 chip：多库/全部时显示「库名 · 文档标题」；单库保持原样式。
   - `openCitationDocument`：先 `await documents.refresh(citation.knowledgeBaseId)` 切到引用所属库，再查找并打开文档；找不到提示「文档可能已被删除或无权访问」。
   - `ConversationSidebar`：单库 scope 按该库过滤；多库/全部显示全部会话；多库会话显示「多库」小标识；`open(item)` 把摘要传给 `chat.loadConversation`。
   - `CitationDrawer`：来源卡片展示知识库名。
6. 迁移：`KnowledgeBaseScopeJson` 用 PostgreSQL `text`，`ScopeAll` 默认 false；存量行不回填。

### 验收

- 选 2 个库提问，引用可来自两个库，chip 带库名。
- 「全部知识库」包含默认库 + 成员库，不包含无权库；勾选无权库直接报错并列出库名。
- 刷新页面打开历史多库会话，scope 恢复并可继续追问。
- 从引用打开文档时工作台切换到正确知识库。
- 单库旧会话（无 scope JSON）行为与 P4 一致。
- 迁移可重复执行（DbMigrator 幂等）。

---

## Task 5：用量记录

**目标**：生成、向量化、重排、改写每次调用都记录 token、耗时与估算费用；记录失败不影响主流程。

### Files

新增：

- `src/KnowVault.Domain.Shared/Enum/Ai/AiOperationEnum.cs`
- `src/KnowVault.Domain/Ai/AiUsageRecord.cs`
- `src/KnowVault.Domain/Ai/AiUsageEntry.cs`
- `src/KnowVault.Domain/Ai/IAiUsageRecorder.cs`
- `src/KnowVault.Domain/Ai/AiPricingOptions.cs`
- `src/KnowVault.Domain/Ai/AiUsageCostCalculator.cs`
- `src/KnowVault.Domain/Embeddings/EmbeddingResult.cs`
- `src/KnowVault.Ai/Rerank/RerankServiceResult.cs`
- `src/KnowVault.EntityFrameworkCore/Ai/AiUsageRecorder.cs`
- `src/KnowVault.EntityFrameworkCore/EntityFrameworkCore/Configurations/AiUsageRecordConfiguration.cs`
- `tests/KnowVault.Tests/AiUsageCostCalculatorTests.cs`
- 迁移：`Migrations/*P5_AiUsageRecords*`

修改：

- `src/KnowVault.Domain/Embeddings/IEmbeddingService.cs`
- `src/KnowVault.Ai/Embeddings/SiliconFlowEmbeddingService.cs`
- `src/KnowVault.Ai/Rerank/IRerankService.cs`
- `src/KnowVault.Ai/Rerank/SiliconFlowRerankService.cs`
- `src/KnowVault.Ai/Rerank/SiliconFlowRerankResponseParser.cs`
- `src/KnowVault.Ai/Rag/RagModels.cs`
- `src/KnowVault.Ai/Rag/RagService.cs`
- `src/KnowVault.Ingest/Processing/DocumentProcessor.cs`
- `src/KnowVault.Application/Qa/QaStreamService.cs`
- `src/KnowVault.EntityFrameworkCore/EntityFrameworkCore/KnowVaultDbContext.cs`
- `src/KnowVault.EntityFrameworkCore/EntityFrameworkCore/KnowVaultEntityFrameworkCoreModule.cs`
- `src/KnowVault.HttpApi.Host/appsettings.json`
- `tests/KnowVault.Tests/RagServiceRerankTests.cs`
- `tests/KnowVault.Tests/RagServiceHybridTests.cs`
- `tests/KnowVault.IntegrationTests/FakeEmbeddingService.cs`
- `tests/KnowVault.IntegrationTests/DocumentLifecycleTests.cs`

### Contracts

```csharp
public enum AiOperationEnum
{
    Chat = 0,
    Embedding = 1,
    Rerank = 2,
    QueryRewrite = 3
}
```

```csharp
public class EmbeddingResult
{
    public required IReadOnlyList<float[]> Vectors { get; init; }
    public required string Model { get; init; }
    public int TotalTokens { get; init; }
    public bool IsEstimated { get; init; }
}

public interface IEmbeddingService
{
    Task<EmbeddingResult> EmbedAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default);
}
```

```csharp
public class RerankServiceResult
{
    public required IReadOnlyList<RerankResult> Results { get; init; }
    public required string Model { get; init; }
    public int TotalTokens { get; init; }
    public bool IsEstimated { get; init; }
}

public interface IRerankService
{
    Task<RerankServiceResult> RerankAsync(string query, IReadOnlyList<string> documents, CancellationToken cancellationToken = default);
}
```

```csharp
public class AiUsageEntry
{
    public Guid? KnowledgeBaseId { get; init; }
    public Guid? ConversationId { get; init; }
    public Guid? AskId { get; init; }
    public Guid? DocumentId { get; init; }
    public AiOperationEnum Operation { get; init; }
    public string Model { get; init; } = string.Empty;
    public int PromptTokens { get; init; }
    public int CompletionTokens { get; init; }
    public int LatencyMs { get; init; }
    public bool IsEstimated { get; init; }
}

public interface IAiUsageRecorder
{
    Task RecordAsync(AiUsageEntry entry, CancellationToken cancellationToken = default);
}
```

```csharp
public class AiPricingOptions
{
    public string Currency { get; set; } = "CNY";
    public decimal DeepSeekInputPerMillion { get; set; } = 2m;
    public decimal DeepSeekOutputPerMillion { get; set; } = 8m;
    public decimal EmbeddingPerMillion { get; set; } = 0.5m;
    public decimal RerankPerMillion { get; set; } = 0.5m;
}
```

```csharp
public static class AiUsageCostCalculator
{
    public static decimal Calculate(
        AiOperationEnum operation,
        int promptTokens,
        int completionTokens,
        AiPricingOptions pricing);
}
```

### Steps

1. 实体与配置：
   - `AiOperationEnum` 放 `Domain.Shared/Enum/Ai/`，文件名与类型名一致。
   - `AiUsageRecord : CreationAuditedEntity<Guid>, IMultiTenant`；表 `AppAiUsageRecords`；字段与 §0.3.2 一致。
   - EF 配置：`Model` 128 必填；`EstimatedCostCny` decimal(18,6)；三个索引。
   - `KnowVaultDbContext` 增加 `DbSet<AiUsageRecord> AiUsageRecords`。
2. 记录器：
   - `AiUsageRecorder` 在 EF Core 项目，实现 `IAiUsageRecorder`；用 `IUnitOfWorkManager.Begin(requiresNew: true, isTransactional: false)` 独立落库。
   - `TotalTokens = Prompt + Completion`；费用按 `AiUsageCostCalculator` 计算。
   - 任何非取消异常只记 warning，不向调用方抛出。
3. AI 契约 usage：
   - Embedding：SiliconFlow 响应有 `usage.total_tokens` 就用；否则 `TokenEstimator` 按输入文本估算并标记 estimated。
   - Rerank：响应有 usage 就用；否则按查询 + 候选文档估算。
   - Chat：T3 已确定流式 usage 可用性；`RagService` 在流结束时用实际值或估算值记录。
   - QueryRewrite：`LlmCompletionResult.Usage` 优先；缺失时估算。
4. 接入点：
   - `DocumentProcessor`：一次 `EmbedAsync` 记录一条 `Embedding`，`DocumentId` 有值、`AskId=null`，按文档聚合。
   - `RagService.RetrieveAsync`：查询 embedding、改写、重排各记录一条；`KnowledgeBaseId` 单库有值、多库 null；`ConversationId` / `AskId` 透传。
   - `RagService` 流式生成结束：记录 `Chat`；prompt 用 system + 历史 + 本轮问题，completion 用回答文本。
   - `QaStreamService`：本轮生成 `AskId` 并放进 `RagRequest`。
5. 价格配置：
   - `AiPricingOptions` 在 EF 模块绑定 `Ai:Pricing`。
   - 默认值按 §0.2；T5 开工当日核对 DeepSeek / SiliconFlow 官网价格，若有变动只改配置与报告说明。
6. 幂等与降级：
   - 用量写入失败不影响问答/摄取；日志可验证。
   - 关闭改写时不产生 `QueryRewrite` 记录；关闭重排时不产生 `Rerank` 记录。
7. 测试：
   - `AiUsageCostCalculatorTests`：Chat（输入 + 输出）、Embedding / Rerank（total）、零价格、六位小数舍入。
   - 更新所有 fake 实现（embedding 返回 `EmbeddingResult`、rerank 返回 `RerankServiceResult`、chat 返回 chunk）。
   - 集成测试：处理一篇文档后 `AppAiUsageRecords` 有该文档的 Embedding 记录。

### 验收

- 提一个问题后数据库出现同一 `AskId` 的多条记录（embedding / rerank / 可选 rewrite / chat），字段与 §0.3.2 一致。
- 上传并处理文档后出现一条该文档的 Embedding 记录。
- 多库提问的问答记录 `KnowledgeBaseId=null`。
- 人为制造记录失败（如临时注入抛错）时问答/摄取仍成功，日志有 warning。
- 迁移可执行，旧数据不受影响。

---

## Task 6：仪表盘用量

**目标**：概览页展示今日与 7 天 token / 成本、按库聚合，并标注估算口径。

### Files

新增：

- `src/KnowVault.Application.Contracts/Models/Dashboard/DashboardUsageDto.cs`
- `src/KnowVault.Application.Contracts/Models/Dashboard/DashboardUsageTrendPointDto.cs`
- `src/KnowVault.Application.Contracts/Models/Dashboard/DashboardKnowledgeBaseUsageDto.cs`
- `src/KnowVault.Application/Dashboard/AiUsageAggregator.cs`
- `tests/KnowVault.Tests/AiUsageAggregatorTests.cs`

修改：

- `src/KnowVault.Application.Contracts/Models/Dashboard/DashboardDto.cs`
- `src/KnowVault.Application/Dashboard/DashboardAppService.cs`
- `frontend/src/components/DashboardPanel.vue`

### Contracts

```csharp
public class DashboardDto
{
    // 现有字段不变
    public DashboardUsageDto Usage { get; set; } = new();
}

public class DashboardUsageDto
{
    public int TodayTokens { get; set; }
    public decimal TodayEstimatedCostCny { get; set; }
    public int Last7DayTokens { get; set; }
    public int AverageTokensPerQuestion { get; set; }
    public bool HasEstimated { get; set; }
    public IReadOnlyList<DashboardUsageTrendPointDto> Last7DayTrend { get; set; } = [];
    public IReadOnlyList<DashboardKnowledgeBaseUsageDto> ByKnowledgeBase { get; set; } = [];
}

public class DashboardUsageTrendPointDto
{
    public DateTime Date { get; set; }
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
    public decimal EstimatedCostCny { get; set; }
}

public class DashboardKnowledgeBaseUsageDto
{
    public Guid? KnowledgeBaseId { get; set; }
    public string KnowledgeBaseName { get; set; } = string.Empty;
    public int QuestionCount { get; set; }
    public int TotalTokens { get; set; }
    public decimal EstimatedCostCny { get; set; }
}
```

### Steps

1. 聚合纯函数 `AiUsageAggregator.Aggregate`：
   - 输入：已筛选的最近 7 天记录、知识库名称字典、今天日期。
   - 输出：今日 tokens/成本、7 天 tokens、平均每次提问 tokens、趋势（空日期补 0）、按库表。
   - 平均每次提问 = 问答类记录 `TotalTokens` 之和 / 不同 `AskId` 数；无问答时为 0。
   - 按库问题数 = 该库下不同 `AskId` 数；`KnowledgeBaseId=null` 归入「多库/全局」。
2. `DashboardAppService`：
   - 注入 `IRepository<AiUsageRecord, Guid>`；查询 `CreationTime >= today - 6 天`。
   - 问答类记录：`AskId != null` 且 `CreatorId == 当前用户`。
   - 摄取类记录：`Operation == Embedding && DocumentId != null && KnowledgeBaseId ∈ 可访问库`。
   - 多库记录：`KnowledgeBaseId == null` 且属于问答类，单列。
   - 知识库名称从 `KnowledgeBase` 批量取；已删除库显示「已删除知识库」。
   - 继续沿用内存聚合；不改现有文档/提问/耗时字段。
3. 前端：
   - 现有 4 张卡与 7 天提问图保持不动。
   - 新增「用量与成本」区：4 张卡（今日 tokens、今日估算成本、7 天 tokens、平均每次提问 tokens）+ 7 天 token 柱状图（tooltip 显示 prompt/completion/cost）+ 按库表格。
   - 引入 ECharts `BarChart`；不新增图表库。
   - 周期内存在 `IsEstimated` 时显示「估算」标签；固定文案「按配置价估算，以账单为准」。
   - 空数据时显示占位，不报错。

### 验收

- 提一个问题后今日 tokens / 成本增加，增量等于该次各阶段记录之和。
- 上传处理文档后对应知识库的 tokens 增加。
- 7 天趋势按天分组，空日期补 0。
- 多库提问进入「多库/全局」行；按库合计 + 多库 = 总计。
- 关闭改写/重排时对应记录不产生，仪表盘不显示该项增量。

---

## Task 7：评估体系

**目标**：6 篇语料 + 30 题金标集 + 三组对比，产出可入仓的 Markdown / JSON 报告。

### Files

新增：

- `tools/KnowVault.Eval/KnowVault.Eval.csproj`
- `tools/KnowVault.Eval/Program.cs`
- `tools/KnowVault.Eval/EvalOptions.cs`
- `tools/KnowVault.Eval/GoldenSetLoader.cs`
- `tools/KnowVault.Eval/AnchorResolver.cs`
- `tools/KnowVault.Eval/EvalRunner.cs`
- `tools/KnowVault.Eval/EvaluationReportWriter.cs`
- `tools/KnowVault.Eval/CollectingUsageRecorder.cs`
- `tools/KnowVault.Eval/Models/GoldenSet.cs`
- `tools/KnowVault.Eval/Models/EvalResult.cs`
- `src/KnowVault.Ai/Evaluation/RankingMetrics.cs`
- `src/KnowVault.Ai/Evaluation/CitationParser.cs`
- `docs/eval/corpus/rag-basics.md`
- `docs/eval/corpus/chunking.md`
- `docs/eval/corpus/vector-search.md`
- `docs/eval/corpus/hybrid-retrieval.md`
- `docs/eval/corpus/rerank.md`
- `docs/eval/corpus/retrieval-metrics.md`
- `docs/eval/golden-set.json`
- `docs/eval/README.md`
- `tests/KnowVault.Tests/RankingMetricsTests.cs`
- `tests/KnowVault.Tests/CitationParserTests.cs`

修改：

- `KnowVault.slnx`
- `src/KnowVault.Domain/VectorStore/IVectorStore.cs`
- `src/KnowVault.EntityFrameworkCore/VectorStore/QdrantVectorStore.cs`
- `tests/KnowVault.IntegrationTests/*`（fake 实现补 `ScrollAsync`）

### Contracts

```csharp
// 评估用：读取指定知识库全部切片，建立 anchor → chunk 映射
Task<IReadOnlyList<VectorSearchHit>> ScrollAsync(
    VectorFilter filter,
    int limit = 10000,
    CancellationToken cancellationToken = default);
```

```csharp
public static class RankingMetrics
{
    public static double RecallAtK(IReadOnlyCollection<Guid> retrieved, IReadOnlyCollection<Guid> gold, int k);
    public static double MrrAtK(IReadOnlyCollection<Guid> retrieved, IReadOnlyCollection<Guid> gold, int k);
    public static double NdcgAtK(IReadOnlyCollection<Guid> retrieved, IReadOnlyCollection<Guid> gold, int k);
}

public static class CitationParser
{
    // 从回答文本提取 [n]，返回 1 起的引用序号（去重、保留出现顺序）
    public static IReadOnlyList<int> Parse(string? answer);
}
```

### Steps

1. 语料：
   - 6 篇原创 Markdown，每篇 800–1500 字，主题按需求 §7.1。
   - 包含精确术语、编号（如 `KV-RRF-60`）、英文缩写、数字，便于构造关键词题。
   - 每篇首行 `# 标题`；上传时使用默认标题（文件名去掉扩展名），评估工具按此匹配 `documentTitle`。
2. 金标集：
   - 30 条：10 关键词 / 10 语义 / 5 跨文档 / 5 不可回答。
   - 每条包含 `id`、`category`、`question`、`relevant[{document, anchor}]`、`answerPoints`、`answerable`。
   - `anchor` 为 10–30 字原文片段；不可回答题 `relevant=[]`、`answerable=false`。
3. 工具组成：
   - 不经过 HTTP / ABP 主机；用 `ServiceCollection` 手动注册 `AiOptions`、`QdrantOptions`、`AiPricingOptions`、`AddHttpClient()`、Kernel、embedding / rerank / chat / rewrite / RagService、QdrantVectorStore、CollectingUsageRecorder。
   - 配置默认读 `src/KnowVault.HttpApi.Host/appsettings.json`，环境变量覆盖；API Key 只从环境变量读取。
   - 前置条件：专用评估知识库已上传 6 篇语料并全部 Ready。
4. anchor 解析：
   - `ScrollAsync` 读取该库全部切片；按 `documentTitle` 匹配语料文件名去扩展名。
   - 对每条 gold，在对应文档切片中查找 `content.Contains(anchor, StringComparison.Ordinal)`；找不到直接中止并列出问题 ID / anchor。
   - 一个 anchor 命中多个切片时全部视为 gold。
5. 三组模式：
   - A `vector`：hybrid off、rewrite off
   - B `hybrid`：hybrid on、rewrite off
   - C `hybrid-rewrite`：hybrid on、rewrite on
   - 重排三组都开；topK 默认 5；`temperature=0`；顺序调用，默认 `--delay-ms 500`。
6. 指标：
   - 检索指标：Recall@5、MRR@10、nDCG@5（只统计可回答问题）。
   - 引用指标：`CitationHitRate`、`CitationPrecision`（只统计至少有一条引用的回答；不可回答题不计入）。
   - 观察项：NoAnswerRate（不可回答题中回答「资料不足/无法回答」的比例）、AvgLatency、Tokens、Cost。
7. 报告：
   - Markdown：元信息（日期、commit、模型名、Qdrant 版本、topK、chunk 参数、重排/混合/改写配置、价格取值日期）、语料清单（文件、标题、字数、SHA256）、三组汇总表、分类表、逐题表、结论、失败案例。
   - 同名 JSON 保存原始结果。
   - 默认输出 `docs/eval/reports/<yyyy-MM-dd>-p5-eval.md`。
8. CLI：
   - `--knowledge-base-id`（必填）、`--golden-set`、`--corpus-dir`、`--modes`、`--top-k`、`--delay-ms`、`--retrieval-only`、`--question-limit`、`--output`。
   - 单题失败记录错误继续；anchor 解析失败中止。
   - 提供 `--retrieval-only` 与 `--question-limit` 便于调试。
9. 指标测试：
   - `RankingMetricsTests`：命中/未命中、排名 1 / 3、nDCG 已知算例、gold 多于 topK。
   - `CitationParserTests`：多个、重复、越界、无引用、`[abc]` 忽略。

### 验收

- 30 题 anchor 全部可解析；不可回答题不参与检索指标。
- 三组报告齐全，指标完整，Markdown + JSON 入仓。
- 报告结论明确写出 B 对 A、C 对 B 的提升/持平/下降及原因；无提升也要写分析。
- `--retrieval-only` 可在不调 DeepSeek 的情况下跑通并生成报告。

---

## Task 8：前端检索设置与体验收尾

**目标**：问答页可切换混合检索 / 查询改写；改写结果可见；多库体验收尾。

### Files

新增：

- `frontend/src/components/RetrievalSettings.vue`

修改：

- `frontend/src/components/ChatPanel.vue`
- `frontend/src/stores/chat.js`
- `frontend/src/components/ConversationSidebar.vue`
- `frontend/src/components/CitationDrawer.vue`

### Steps

1. `chat` store：
   - `hybridEnabled` / `rewriteEnabled` 初值从 localStorage 读取（键 `knowvault.retrieval.hybrid` / `knowvault.retrieval.rewrite`，默认 `true` / `false`）。
   - `setHybrid` / `setRewrite` 同步写 localStorage。
   - `ask` / `regenerate` 的 payload 带上两个布尔值。
   - `loadConversation` 恢复 scope；`Started` 帧回填实际 `knowledgeBaseIds` 与 `rewriteQueries`。
2. `RetrievalSettings.vue`：
   - 弹出层两个开关；改写开关旁提示「每次提问会多一次模型调用」。
   - 纯展示组件，状态由 `chat` store 持有。
3. `ChatPanel.vue`：
   - 头部新增「检索设置」按钮，挂载 `RetrievalSettings`。
   - 助手消息上方显示本次改写查询（`RewriteQueries` 非空时），可折叠或单行省略。
   - 多库引用 chip 显示库名；单库保持现状。
   - 发送 / 重发生成使用 chat scope，不再读 `documents.knowledgeBaseId`。
4. `ConversationSidebar.vue`：
   - 按 chat scope 过滤：单库过滤、多库/全部不过滤。
   - 多库会话显示「多库」标识。
5. `CitationDrawer.vue`：
   - 来源信息增加知识库名；无名称时不显示该行。
6. 回归走查 P4 验收项：会话切换、反馈、复制、重生成、引用抽屉、文档详情、批量上传。

### 验收

- 关闭两个开关后，请求不再触发 sparse / rewrite（看网络与服务端日志）。
- 开启改写的回答上方能看到变体查询。
- 刷新页面后两个开关状态保持。
- P4 的问答、会话、引用、文档预览全部正常。
- `npm run build` 通过。

---

## Task 9：全量验收与文档回写

**目标**：逐项实测需求 §11，回写文档，留下可复查的证据。

### Files

修改：

- `TECH_STACK.md`（P5 标记完成；§9.7 与最终实现对齐）
- `AGENTS.md`（P5 状态、关键约定、验收结论）
- `docs/plans/2026-10-24-knowvault-open-source-roadmap.md`（P5 标记已完成）
- `docs/driver/需求/需求分析-1027-P5.md`（状态「已验收」）
- `docs/P5-代码导读.md`（新增，只写 P5 增量）
- `docs/eval/reports/<日期>-p5-eval.md` + `.json`（最终报告）

### Steps

1. 按 §2 验收表逐项实测；每项记录命令、结果、截图或日志位置。
2. 质量门禁：
   ```bash
   dotnet build KnowVault.slnx
   dotnet test
   cd frontend && npm run build
   dotnet format style --verify-no-changes --diagnostics IDE1006
   ```
3. 行尾 / BOM / 密钥自查：
   - 新增与修改的文本文件 CRLF；`Dockerfile*` / `*.sh` 保持 LF。
   - UTF-8 无 BOM。
   - 仓库无真实密钥；评估语料与报告无敏感信息。
4. 文档：
   - `docs/P5-代码导读.md` 只写 P5 增量：新增文件地图、检索链路读法、用量链路读法、多库链路读法、评估复现命令、验收记录。
   - `TECH_STACK.md` §6 / §9.7 与实际实现一致。
   - `AGENTS.md` 记录 P5 完成状态与后续注意事项（如旧 collection 必须重建）。
5. 评估报告：
   - 用最终代码跑一遍三组；报告入仓；在 `docs/eval/README.md` 中给出复现命令与报告链接。

### 验收

- §2 验收表全部通过。
- 四条质量门禁全绿。
- 文档链接有效，报告可被 README 直接引用。
- 工作区无临时 spike 文件、无调试日志、无密钥。

---

## §2 端到端验收

| 编号 | 验收项 | 操作 | 通过标准 |
|---|---|---|---|
| A1 | 混合开关 | 关闭 `HybridEnabled` 提问 | 行为与 P4 一致：只走 dense，引用顺序与旧结果相同 |
| A2 | 关键词召回 | 提含精确编号 `KV-RRF-60` 的问题 | 混合模式命中；纯向量模式不命中或排名更低 |
| A3 | 权重可配 | `KeywordWeight=0` / `DenseWeight=0` 各跑一次 | 前者等价纯 dense；后者只返回关键词命中 |
| A4 | 稀疏降级 | 临时让 sparse 检索抛错 | 问答成功，日志 warning，回答仍基于 dense |
| A5 | 改写关闭 | 关闭改写提问 | 无改写调用；Started 的 `RewriteQueries` 为空 |
| A6 | 改写开启 | 开启改写提问 | 最多 2 条变体，原问题第一；Started 可见 |
| A7 | 改写失败 | 断网或返回非 JSON | 问答成功，前端无报错 |
| A8 | 多库 | 选 2 个库提问 | 引用可来自两个库，chip 带库名 |
| A9 | 全部库 | 选「全部知识库」 | 包含默认库 + 成员库，不含无权库 |
| A10 | 无权拒绝 | 勾选一个无权库 | 明确报错并列出库名，不静默过滤 |
| A11 | scope 恢复 | 刷新后打开多库会话并追问 | scope 正确恢复，追问沿用该集合 |
| A12 | 引用跳转 | 从多库引用打开文档 | 工作台切到引用所属库并打开文档 |
| A13 | 评估 | 跑三组 30 题 | 报告齐全，结论明确，anchor 全部可解析 |
| A14 | 仪表盘 | 提问 + 上传后看概览 | 今日/7 天/按库 tokens 与成本正确；多库单列 |
| A15 | 摄取用量 | 上传并处理文档 | 对应知识库出现 Embedding 用量 |
| A16 | 旧 collection | 用 P4 collection 启动并检索 | 明确提示重建；执行重建后所有 Ready 文档可检索 |
| A17 | 回归 | 走查 P4 十步 | 账户、会话、引用、文档、反馈、重生成、设置均正常 |

### 质量门禁

- `dotnet build KnowVault.slnx` 0 错误 0 警告
- `dotnet test` 全部通过
- `npm run build` 通过
- `dotnet format style --verify-no-changes --diagnostics IDE1006` 无新增违规
- 新增/修改文件 CRLF、UTF-8 无 BOM；`Dockerfile*` / `*.sh` 保持 LF
- 仓库无真实密钥；评估语料与报告无敏感信息

---

## §3 风险与缓解

| 风险 | 影响 | 缓解 |
|---|---|---|
| bigram 噪声导致关键词召回质量不佳 | 混合指标不如纯向量 | 最小停用词 + 重排兜底；评估分关键词/语义类；若仍差，P5 后再评估 Jieba，不保留双实现 |
| Qdrant.Client 1.19 稀疏写法与预期不符 | T1 卡住 | T1 先做 10 行 spike 确认 `Query` / `Vectors` 写法；必要时把 dense 也改为命名向量（`dense`）并重建，仍只保留一套实现 |
| 旧 collection 不能原地加稀疏向量 | 升级失败 | 只提供显式 Admin 重建接口；不自动删库；文档写清维护窗口 |
| 重建期间问答不可用 / 请求超时 | 演示中断 | 开发数据量小；在维护窗口执行；接口逐文档重新入队，可重复执行 |
| 流式 usage 取不到 | 成本为估算值 | T3 spike 先验证；`IsEstimated=true`，仪表盘明示；估算不影响问答 |
| 价格变动 | 成本数字失真 | 价格配置化；报告记录取值日期；文案注明「估算，以账单为准」 |
| 评估依赖 LLM 生成，结果波动 | 引用指标不稳定 | `temperature=0`；记录模型版本；检索指标不受影响；报告注明限制 |
| 多库上下文稀释 | 单库答案质量下降 | 统一重排；不做按库配额；验收以引用为准；P6 再优化 |
| 多库 scope 重构影响 P4 会话/工作台 | 回归 | scope 放 chat store，不动 `documents.knowledgeBaseId` 的工作台语义；T8 走查 P4 验收项 |
| 用量记录写入拖慢问答 | 延迟增加 | 每次操作独立小事务；记录失败不阻断；作品规模可接受，P6 再批量/异步 |
| `ReprocessAsync` 逐文档调用较慢 | 重建耗时长 | 开发库数据量小；接口返回计数；必要时分批调用 |
| ABP 动态 API 路由变化 | 前后端联调失败 | 沿用 P2 实测规则；T1/T4 完成后用 Swagger 核对路由；前端同步改 |
| 范围膨胀 | P5 延期 | 新想法进路线图可选区，不插队 |

---

## §4 完成定义（DoD）

- 需求 §11 的 16 项功能验收与 §2 的 A1–A17 全部通过并留证。
- 四条质量门禁全绿。
- 新增单测覆盖切词、RRF、改写解析、token 估算、费用、聚合、排序指标、引用解析；现有 35 个单测与 3 个集成测试回归通过。
- 三组评估报告入仓，结论明确可解释。
- `TECH_STACK.md`、`AGENTS.md`、路线图、需求文档、`docs/P5-代码导读.md`、`docs/eval/README.md` 同步完成。
- 仓库无临时 spike 文件、无调试日志、无真实密钥；行尾与编码合规。

---

## 确认清单

请确认以下内容后我再开始 T0；确认前不改任何代码。

1. **D1–D6** 是否全部按需求文档 §14 的推荐执行（Qdrant 稀疏 + 应用层 RRF、bigram、混合开/改写关、多选+全部、自写 6 篇语料、Admin 重建）。
2. **范围与工期**：接受 T0–T9 共约 9–13 人日；若必须压到 6–10 人日，优先把评估语料减到 4 篇、仪表盘图表简化为表格。
3. **破坏性 API 变更**：删除 `AskQuestionInput.KnowledgeBaseId`，改为 `KnowledgeBaseIds` + `AllKnowledgeBases`；仓库内唯一调用方是前端，同步修改。
4. **一次性重建**：接受升级时执行 Admin 重建接口并重新处理全部 Ready 文档；不接受就不做稀疏向量方案。
5. **三个计划级取舍**：
   - 多库提问不使用单库自定义系统提示词；
   - `Completed` 帧不新增 token / 成本（只在仪表盘看）；
   - 用量记录每次操作独立小事务，记录失败只记日志。
