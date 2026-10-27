# KnowVault / 智库 — 技术栈与架构定稿

> 一句话定位：**你问，它从你的文档里找答案。**
> 一个基于 RAG 的个人 AI 知识库：上传私有文档 → 系统解析、切片、向量化 → 用自然语言按"语义"检索 → 模型严格引用你的资料作答并标注出处。

---

## 1. 已确认技术栈

| 层 | 选型 |
|---|---|
| 后端框架 | **.NET 10 (LTS)** + **ABP.VNext 10.x** |
| AI 编排 | **Semantic Kernel** (.NET 1.x 稳定线) |
| 大模型 (生成) | **DeepSeek**（`deepseek-chat`） |
| Embedding + Rerank | **SiliconFlow / 硅基流动**（`BAAI/bge-m3` + `BAAI/bge-reranker-v2-m3`） |
| 向量数据库 | **Qdrant**（自托管，Docker 一键起） |
| 主库 / ORM | **PostgreSQL** + EF Core |
| 缓存 | **Redis** |
| 对象存储 | **MinIO**（原始上传文档，S3 兼容） |
| 异步队列 | **RabbitMQ** / **MassTransit 8.5.7（Apache-2.0）**（9.x 为 Massient 商业许可，勿用） |
| 可观测 | **OpenTelemetry** + **Seq** + Serilog |
| 实时输出 | **SignalR**（AI 流式"打字机"效果） |
| 本地编排 | **.NET Aspire** |
| 测试 | **xUnit** + **Testcontainers** + FluentAssertions |
| 前端 | **Vue 3** + Vite + **JS**（不用 TS）+ **Naive UI** + Pinia + Axios + ECharts |
| DevOps | **Docker Compose**（CI 暂未接入，后续考虑 Gitee Go） |

---

## 2. 模型分工（跨厂商混合路由）

- **生成（回答）**：DeepSeek `deepseek-chat` —— 省钱、中文/推理好。
- **向量化（Embedding）**：SiliconFlow（硅基流动）`BAAI/bge-m3` —— 1024 维，语义检索的"坐标"。
- **重排（Rerank）**：SiliconFlow `BAAI/bge-reranker-v2-m3` —— 提升检索召回。

> 选型说明：DeepSeek 官方未稳定提供 embedding/rerank，因此这三环不能全靠 DeepSeek；
> embedding + rerank 由 SiliconFlow 统一承担，生成仍走 DeepSeek。跨厂商各取所长。
> **换 embedding 模型必须重建向量库**（维度或语义空间变化会导致检索失真）；只换 rerank 模型不需要重新向量化。

---

## 3. 架构 / 解决方案形态（ABP 分层 + AI 层）

```
KnowVault.slnx
├─ src/
│  ├─ KnowVault.Domain.Shared         // 共享枚举 / 常量 / 本地化资源（跨层复用的类型放这里）
│  ├─ KnowVault.Domain                // 实体 / 聚合 / DomainEvent / 仓储接口（多租户预留）
│  ├─ KnowVault.Application.Contracts // AppService 契约 + DTO（上传/问答等入参出参）
│  ├─ KnowVault.Application           // AppService 用例编排 + AI 编排入口
│  ├─ KnowVault.EntityFrameworkCore   // EF Core(PostgreSQL) + DbContext / EF 迁移；Qdrant 与 MinIO 访问实现（接口定义在 KnowVault.Domain）
│  ├─ KnowVault.Ai                   // Semantic Kernel：Prompt / RAG / 向量检索 / 重排 / 流式
│  ├─ KnowVault.Ingest               // 文档解析后台 Worker（消费 RabbitMQ，产出切片+向量）
│  ├─ KnowVault.HttpApi              // 动态 API 控制器（ABP 自动生成）
│  ├─ KnowVault.HttpApi.Client       // HttpClient 强类型代理（供外部调用方使用）
│  ├─ KnowVault.HttpApi.Host         // 主宿主（OpenIddict/健康检查/Swagger），Aspire 编排入口
│  └─ KnowVault.DbMigrator           // 数据库迁移 / 种子执行器（EF 迁移入口）
├─ aspire/
│  └─ KnowVault.AppHost              // Aspire 编排：Postgres/Redis/Qdrant/MinIO/RabbitMQ/Seq + API
├─ frontend/                          // Vue3 + Vite + JS + Naive UI
└─ tests/                             // KnowVault.Tests（纯函数单测）+ KnowVault.IntegrationTests（P3 Testcontainers）
```

> **说明（当前实现）**：以上是 P0 用 ABP 分层模板**实际落地**的结构（`KnowVault.slnx`）。相比早期设想，主宿主为 ABP 约定的 `KnowVault.HttpApi.Host`（而非 `KnowVault.Web`），并多出 `Domain.Shared` / `Application.Contracts` / `HttpApi.Client` / `DbMigrator` 等标准层；`tests/` 在 **P1** 先落 `KnowVault.Tests`（纯函数单测），Testcontainers 集成测试仍留 P3。

---

## 4. 数据模型（单租户起步，结构预留多租户）

> 采用「**结构按多租户设计、先只跑一个租户**」策略；ABP 多租户是一等公民，
> 后续开多租户 = 开开关，数据模型无需返工。所有表都带 `TenantId`。

- **KnowledgeBase 知识库**：`Id, Name, Description, TenantId, CreatedAt`
- **Document 文档**：`Id, KnowledgeBaseId, Title, SourceFileName, SourceType, ObjectStorageKey, Status(Pending/Processing/Ready/Failed), ErrorMessage, CreatedAt`
- **DocumentChunk 切片**：`Id(==Qdrant pointId), DocumentId, ChunkIndex, Content` —— 内容与溯源信息存进 Qdrant payload；文档元数据存 PostgreSQL
- **Conversation 会话**：`Id, Title, TenantId, CreatedAt`
- **Message 消息**：`Id, ConversationId, Role(User/Assistant), Content, CitationsJson, CreatedAt`
- **租户/用户**：由 ABP 的 Identity + 租户模块提供

> 权限：P3 已落 **系统角色（Admin/Member/Viewer）+ 知识库成员（Owner/Editor/Viewer）** 双层控制，默认知识库对所有登录用户开放写权限，其余知识库按成员表校验；详见 §9.3。

---

## 5. MVP 范围（第一版：最小问答闭环）

**只做这一条闭环，跑通为止：**

```
上传文档 → 解析(PDF/Word/Excel/Markdown/网页) → 切片 → SiliconFlow 向量化
   → 写入 Qdrant（带文档/知识库元数据）
用户提问 → SiliconFlow 向量化 → Qdrant 语义检索(top-k)
   → 组装 Prompt(top-k 切片 + 问题) → DeepSeek 生成
   → 返回答案 + 引用出处
```

**MVP 含**：ABP 开箱登录（用户名/密码）+ 会话/消息存储（含引用溯源）。

**MVP 不含**（放二期）：多知识库管理界面、管理员控制台统计、多轮对话交互历史、混合检索(BM25)+重排、角色/知识库权限、OCR、外部数据源导入、公网部署/开源。

---

## 6. 分阶段路线图

| 阶段 | 内容 | 产出 |
|---|---|---|
| **P0 骨架** | 建解决方案（ABP 分层）+ Aspire 编排（Postgres/Redis/Qdrant/MinIO/RMQ/Seq）+ Vue3 脚手架 + 最小上传/问答 API | 项目能跑起来，界面能连上后端 |
| **P1 最小闭环** | 上传→解析→切片→向量化→Qdrant 写入；提问→检索→DeepSeek→引用溯源；文档解析 Worker 接 RabbitMQ | ✅ MVP 达成，可 demo |
| **P2 快速加分** | SignalR 流式输出、Rerank 重排、多轮会话历史、知识库管理界面 | ✅ 已完成（2026-10-14 端到端验收 15 项通过，🔥 demo 效果拉满）：见 [`docs/plans/2026-10-14-knowvault-p2-mvp.md`](docs/plans/2026-10-14-knowvault-p2-mvp.md) |
| **P3 深度/工程** | OpenTelemetry 全链路、Testcontainers 集成测试、角色+知识库权限 | ✅ 已实现（2026-10-18）：见 [`docs/plans/2026-10-18-knowvault-p3-mvp.md`](docs/plans/2026-10-18-knowvault-p3-mvp.md)；Seq/权限/重试需按该文档 §2 手工验收 |
| **P4 演示完整度** | 账户体验、会话管理、引用原文、文档/切片预览、批量上传、反馈与重生成、库设置、轻量仪表盘、知识库工作台 | ✅ 已完成（2026-10-26）：见 [`docs/plans/2026-10-24-knowvault-p4-mvp.md`](docs/plans/2026-10-24-knowvault-p4-mvp.md)；代码导读见 [`docs/P4-代码导读.md`](docs/P4-代码导读.md) |
| **P5 检索含金量** | 混合检索 + RRF、查询改写、多库问答、评估报告、成本统计 | ✅ 已完成（2026-10-27）：见 [`docs/plans/2026-10-27-knowvault-p5-mvp.md`](docs/plans/2026-10-27-knowvault-p5-mvp.md)、[`docs/P5-代码导读.md`](docs/P5-代码导读.md)；三组评估结论见 [`docs/eval/reports/2026-10-27-p5-eval.md`](docs/eval/reports/2026-10-27-p5-eval.md)（本语料判为持平） |
| **P6 开源交付** | Docker Compose 一键启动、README/截图、LICENSE、示例数据、Gitee Go（可选） | 待实施（同上路线图） |

---

## 7. 本地开发环境

- **.NET 10 SDK** + Node.js（Vite）
- **Docker Desktop**（跑 Postgres / Redis / Qdrant / MinIO / RabbitMQ / Seq）
  - 不用 Aspire 时（本机验收路径）：复用 P1 遗留的手工容器 `kv-postgres` / `kv-redis` / `kv-qdrant` / `kv-rabbitmq` / `kv-minio`，`docker start kv-*` 即可（数据在容器可写层）；**Postgres 映射在宿主 5433**；仓库内**没有** docker-compose 文件，配置走环境变量覆盖 `ConnectionStrings__Default` / `ConnectionStrings__rabbitmq` / `Minio__*` / `Redis__Configuration` / `Qdrant__Endpoint`
- **Aspire** 一键编排启动全部依赖
- 模型：`DeepSeek` + `SiliconFlow` 的 **API Key**（环境变量，不进仓库）
- 可选：本地 Ollama 用于离线 fallback

---

## 8. 待确认的开放项

- [x] DeepSeek / SiliconFlow 的 Key 已确认会配置（P1 端到端真跑；Key 走环境变量 `Ai__*`，严禁进仓库）
- [ ] 前端首期用 `Naive UI` 的中文主题模板，还是从零写

---

## 9. 实现细节定稿（动手前已敲定）

- **项目初始化（P0 实际落地）**：用 **ABP Studio CLI** 执行 `abp new KnowVault -t app -u no-ui -d ef -dbms postgresql`（CLI 无 `--aspire` 参数；Aspire 版 `aspire/KnowVault.AppHost` 为手工搭建，负责 Postgres/Redis/Qdrant/MinIO/RabbitMQ/Seq 编排；前端另走 Vue3）
- **文档解析（逐格式专库）**：PDF=`PdfPig`、Word=`.docx`→`DocumentFormat.OpenXml`、Excel=`.xlsx`→`NPOI`/`ClosedXML`、Markdown=`Markdig`、HTML=`HtmlAgilityPack`
- **Qdrant**：单一 collection + payload 过滤（每个向量带 `tenantId / knowledgeBaseId / documentId / chunkIndex`），不按知识库建多个 collection
- **切片 Chunking**：按段落/标题层级 + 固定长度，初值 chunk≈500–800 token、overlap≈50–100，可配置；保留段落/标题 metadata 供引用溯源
- **检索 + 组装**：按 `tenantId + knowledgeBaseId` 做 payload 过滤，top-k≈5–8，二期加 rerank；System prompt 规定「只根据资料回答、不足就说不知道、标注来源」，top-k 切片 + 问题 → DeepSeek → answer + citations
- **认证**：MVP 纳入 ABP 开箱登录（用户名/密码）
- **会话存储**：从第一天存 `Conversation` + `Message`（含 `CitationsJson`），为二期多轮/历史打底

### 9.1 P1 实现细节定稿（2026-10-05）

- **消息**：`MassTransit 8.5.7`（Apache-2.0）+ `MassTransit.RabbitMQ`；**进程内 consumer**（Host 内消费，队列仍走 RabbitMQ），consumer/processor 写在 `KnowVault.Ingest` 且保持宿主无关，P3 可平移为独立 Worker。
  - ⚠️ **禁止升到 9.x**：9.2.3 的 nuspec 无 license 表达式、只有 `licenseUrl=https://massient.com/license`（Massient 商业产品）；8.5.7 为 Apache-2.0 且带 `net10.0` 目标组。
- **向量库**：官方 `Qdrant.Client 1.19`；接口 `IVectorStore` 在 `KnowVault.Domain`，实现 `QdrantVectorStore` 在 `KnowVault.EntityFrameworkCore`；单一 collection `knowvault_chunks`，1024 维（当前为 `BAAI/bge-m3`）、Cosine；gRPC 端口 **6334**（不是 REST 的 6333）。
- **payload 字段**：`tenantId / knowledgeBaseId / documentId / chunkIndex / content / documentTitle / headingPath / sourceType`；**`tenantId` 为 null 时归一化为 `Guid.Empty`**（Qdrant 无法 match null，单租户运行时必然命中这一条）；pointId = `DocumentChunk.Id`（UUID 字符串）。
- **对象存储**：`Minio` SDK 自封装 `IDocumentStorage`（接口在 Domain，实现在 EntityFrameworkCore），bucket `knowvault`，对象键 `{tenantId|host}/{knowledgeBaseId}/{documentId}{ext}`；**不用** ABP BlobStoring 的 S3 兼容 provider。
  - 说明：MinIO 服务端是 AGPL-3.0，但仓库只通过 Docker 运行**未修改的官方镜像**、用自己 Apache-2.0 的 SDK 调用，不构成对仓库代码的许可传染。
- **解析库**：PDF=`PdfPig`、Word=`.docx`→`DocumentFormat.OpenXml`、Excel=`.xlsx`→**`ClosedXML`（弃 NPOI）**、Markdown=`Markdig`、HTML=`HtmlAgilityPack`；P1 只支持**上传 .html 文件**，**不做 URL 抓取**。
- **切片单位**：以**字符数近似 token**（默认 1000 字符 / overlap 120，≈600 / 70 token 量级）——中文没有可靠 .NET tokenizer，不引 tiktoken 类依赖；超长单段按句子边界硬切、此时不叠加 overlap。
- **知识库**：P1 不做管理界面，由 DbMigrator 种子固定 ID 的「默认知识库」，并提供最小列表 API 供前端下拉。
- **认证**：SPA 走 Resource Owner Password grant，复用已种子的公开客户端 `KnowVault_App`；API 侧类级 `[Authorize]`，权限细分留二期。
- **P1 不做**：rerank、多轮历史加载、混合检索(BM25)、删除文档与 Qdrant points 清理、失败自动重试/"重新处理"、流式输出、URL 抓取、权限细分。
- **测试**：P1 只加 `tests/KnowVault.Tests` 的**纯函数单测**（切片/解析/payload/Prompt），Testcontainers 集成测试仍留 P3；PDF 解析因缺可复现二进制夹具改手工验收。
  - ⚠️ `FluentAssertions` 若引入**必须钉 7.x**（7.2.0 为 Apache-2.0；8.11.0 已改为许可文件即 Xceed 商业 EULA）。

### 9.2 P2 实现细节定稿（2026-10-14）

- **流式输出**：`SignalR` 服务端在 .NET 10 共享框架内（**零 NuGet 包**），前端用 npm `@microsoft/signalr ^10.0.11`（MIT）；Hub 路由 `/signalr/qa`，方法名 `Ask`，返回 `IAsyncEnumerable<QaStreamEventDto>`（`Started` / `Delta` / `Completed` 三帧），客户端用 `connection.stream()` 消费。
- **SignalR 鉴权**：WebSocket 无法携带自定义请求头，JS 客户端只能把令牌放 query string；而 **OpenIddict 的验证处理不读取 query string**（实测 `openiddict.validation.aspnetcore 7.5.0` 的 DLL 里 `access_token` 命中 0 次）→ Host 在 `UseAuthentication()` **之前**插一层中间件，只对 hub 路径把 `?access_token=` 搬运成 `Authorization: Bearer`。
- **挂 Hub 的位置**：ABP 提供 `UseConfiguredEndpoints(IApplicationBuilder, Action<IEndpointRouteBuilder>)` 重载（10.5.0 XML 文档核实）→ 用 `app.UseConfiguredEndpoints(endpoints => endpoints.MapHub<QaHub>(QaHub.Route))`，不要再写 `UseEndpoints`。
- **重排（Rerank）**：SiliconFlow `POST {SiliconFlowBaseUrl}/rerank`，模型 **`BAAI/bge-reranker-v2-m3`**（2026-10-23 由 Qwen `qwen3-rerank` 切换而来）；响应为顶层 `results[{index, relevance_score}]`，解析后按分数降序。
- **重排与 Embedding 同源**：共用 `Ai:SiliconFlowBaseUrl`（`https://api.siliconflow.cn/v1`）与同一个 Bearer Key；调用失败或返回空时 **fail-open** 退回向量检索顺序。
- **重排策略**：宽召回 + 精排（`RerankCandidateCount = 20` → 取 topK ≤ 8），默认开启；请求把**全部候选**交给服务端打分（`top_n` = 候选数），最终条数由 `RagService` 本地 `Take(topK)` 裁剪——对端是否按其 `top_n` 语义裁剪不可依赖；引用中的 `Score` 语义由「余弦相似度」变为「重排相关性分（0~1，仅请求内相对可比）」。
- **多轮历史**：只做「Prompt 带最近 `HistoryMaxMessages = 6` 条历史」（3 轮往返），**不做**会话列表侧边栏；历史以独立消息注入（最后一条用户消息仍是「资料片段 + 问题」），渲染前剥掉助手旧回答里的 `[编号]` 标记，System prompt 增加「历史只用于理解指代，事实依据必须来自本次资料片段」。
- **流式实现**：`ILlmChatService` 只保留 `CompleteStreamAsync`（SK `GetStreamingChatMessageContentsAsync`）；REST 问答入口改为把同一条流拼成整段文本，两条入口共用一份检索 / 重排 / 落库实现。
- **UoW 与流式的冲突**：ABP 的 UoW 拦截器在方法返回时就结束工作单元，而 `async IAsyncEnumerable` 的真正工作发生在枚举期间 → 流式编排服务**不依赖环境 UoW**，每次落库显式 `Begin(requiresNew: true)`；该类用 `[RemoteService(IsEnabled = false)]` 保证不被生成动态 API 控制器。
- **知识库管理**：新增 Create / Update / Delete；**删除仅限空库**（默认知识库与含文档的库一律拒绝），文档删除与 Qdrant points 清理留 P3；错误提示沿用 `UserFriendlyException` + 中文文案（与 `DocumentAppService` 一致）。
- **P2 不做**：会话列表 / 管理 API、文档删除、MinIO 孤儿对象清理、混合检索(BM25)、重排结果落库、多租户隔离强化、OCR、URL 抓取、Ingest 进度实时推送（前端仍 2s 轮询）、Testcontainers 集成测试。
- **测试**：延续 P1，只加 `tests/KnowVault.Tests` 的纯函数 / 手写 fake 单测（重排响应解析、重排降级、Prompt 历史渲染）。
- **ABP 动态 API 路由（实测结论，写前端 URL 必须照它来）**：`UpdateAsync(输入DTO)` → `PUT /api/app/{服务名}`，**路由不带 `{id}`、实体 ID 走请求体**（Task 8 实测拼 `{id}` 会 405）；`DeleteAsync(Guid id)` → `DELETE /api/app/{服务名}/{id}`；`CreateAsync` → `POST /api/app/{服务名}`。
- **API 认证失败语义**：`/api` 前缀下把 Cookie 方案的 challenge 改写成 **401 + `WWW-Authenticate: Bearer`**（其它路径保持跳登录页），前端 axios 据此清 token 回登录态。
- **执行计划**：见 `docs/plans/2026-10-14-knowvault-p2-mvp.md`

### 9.3 P3 实现细节定稿（2026-10-18）

- **可观测**：Serilog 日志经 `Serilog.Sinks.Seq` 直送 Seq；OpenTelemetry trace 用 **OTLP/HTTP protobuf** 送 Seq 的 `/ingest/otlp/v1/traces`（Seq 本地 gRPC 需 HTTPS，不能走 HTTP）；EF Core 插桩包锁 `OpenTelemetry.Instrumentation.EntityFrameworkCore 1.17.0-beta.1`（beta，目标 net10.0）。自定义 `ActivitySource` 名 `KnowVault`，span：`qa.ask` / `rag.retrieve` / `embedding.embed` / `llm.complete` / `rerank.rerank` / `ingest.process`。
- **权限双层**：系统角色 `Admin` / `Member` / `Viewer` 由 `KnowVaultIdentityDataSeedContributor` 种子并授权；知识库成员表 `AppKnowledgeBaseMembers`（`KnowledgeBaseId + UserId` 唯一，角色 `Viewer=1 / Editor=2 / Owner=3`）。`KnowledgeBaseAccessChecker` 统一判定：Admin 短路；默认知识库对所有登录用户等价 Editor；其余库查成员表，越权抛 `AbpAuthorizationException`。
- **成员管理**：`IKnowledgeBaseMemberAppService` 提供列表/设置/移除；仅 Owner 可操作；默认库拒绝；删除最后一个 Owner 拒绝。
- **文档生命周期**：`DELETE /api/app/document/{id}`（Qdrant points → chunks → document → MinIO 原文件；MinIO 失败只 warning）与 `POST /api/app/document/{id}/reprocess`（清旧向量/切片 → `MarkPending` → 提交后发消息）。`Document.IngestAttemptCount` 记录尝试次数。
- **摄入重试**：`document-ingest` 端点显式 `UseMessageRetry(5 × 2s)`；Consumer 在**独立 UoW** 落 `Failed` 后 rethrow，耗尽后进 `document-ingest_error`。
- **集成测试**：`tests/KnowVault.IntegrationTests` 用 Testcontainers 起 Postgres/RabbitMQ/MinIO/Qdrant；Embedding 与消息发布用 fake；`AbpIntegratedTest` 服务级测试，`AddAlwaysAllowAuthorization` 绕过授权拦截器但保留 ACL 真实校验。本机 MinIO 镜像不可达时可设 `KNOWVAULT_SKIP_MINIO=1` 用内存存储兜底，集成测试不设该变量。
- **CI**：暂未接入（不使用 GitHub Actions；后续考虑迁移到 Gitee Go）。
- **执行计划**：见 `docs/plans/2026-10-18-knowvault-p3-mvp.md`

### 9.4 文档去重定稿（2026-10-23）

- **范围**：同一知识库内按**原始文件名（含扩展名，忽略大小写）**去重；不同知识库允许同名文件。
- **行为**：上传前查询 `Document.SourceFileName`，命中直接拒绝（`UserFriendlyException`），提示已有文档标题与上传时间，引导删除旧文档或使用「重新处理」；不覆盖、不批量跳过，也不写入对象存储。
- **不做**：文件内容 hash / 近似去重留后续优化；存量文档 `SourceFileName` 为空且不回填，因此旧文档不会被新上传命中。
- **存储**：`AppDocuments` 新增可空 `SourceFileName`（varchar 256）与 `(KnowledgeBaseId, SourceFileName)` 索引；文件名先经 `Path.GetFileName` 去掉浏览器可能带上的路径。

### 9.5 Embedding / Rerank 切换 SiliconFlow（2026-10-23）

- **范围**：向量化与重排统一改走 SiliconFlow，Qwen / DashScope 的代码与配置直接移除（不保留双实现）；生成仍用 DeepSeek。
- **模型**：Embedding=`BAAI/bge-m3`（1024 维，与 Qdrant 的 `VectorSize=1024` 一致）；Rerank=`BAAI/bge-reranker-v2-m3`。
- **端点**：两者共用 `Ai:SiliconFlowBaseUrl=https://api.siliconflow.cn/v1`，分别为 `POST /embeddings` 与 `POST /rerank`，鉴权均为 `Authorization: Bearer {SiliconFlowApiKey}`。
- **请求差异**：Embedding 与 OpenAI 兼容（`model/input/encoding_format`），**不能传 `dimensions`**（bge-m3 不支持，会 400）；Rerank 响应是顶层 `results[{index,relevance_score}]`，不是 `output.results`。
- **配置键**：`Ai:SiliconFlowApiKey` / `SiliconFlowBaseUrl` / `SiliconFlowEmbeddingModel` / `SiliconFlowRerankModel`；本地 `.env` 对应 `SILICONFLOW_API_KEY` / `SILICONFLOW_BASE_URL` / `SILICONFLOW_EMBEDDING_MODEL` / `SILICONFLOW_RERANK_MODEL`，由 AppHost 映射为 `Ai__*`。
- **不需要数据迁移**：切换时历史文档已删除；今后如需更换 embedding 模型（维度或语义空间变化）必须重新向量化，只换 rerank 模型则不需要。
- **执行计划**：见 [`docs/plans/2026-10-23-siliconflow-model-switch.md`](docs/plans/2026-10-23-siliconflow-model-switch.md)

### 9.6 P4 实现细节定稿（2026-10-26）

- **账户**：注册走自定义 `AccountAppService` + `IdentityUserManager`，新账号默认加入 `Member`；个人中心提供显示名/邮箱与改密，改密后当前令牌保持有效；显示名保存在 `IdentityUser.Name`，登录后同步到右上角。
- **会话**：`Conversation` 增加 `KnowledgeBaseId` 与 `LastMessageAt`，按用户 + 知识库列出；所有读写统一走 `GetOwnedConversationAsync` 归属校验；首次提问自动用问题截断作标题，支持重命名/删除，刷新后从侧栏恢复并继续追问。
- **引用溯源**：`RagCitationDto` 增加 `Snippet` 与 `HeadingPath`；`Message.CitationsJson` 由 `varchar(4000)` 改为 `text`（迁移 `P4_MessageCitationsText`），避免长引用截断；前端引用抽屉展示命中片段并可跳转文档详情。
- **文档详情与预览**：新增切片列表与原件内容接口；PDF/HTML/Markdown 用 blob + object URL 在线预览，Word/Excel 仅下载；HTML 预览 iframe 的 `sandbox` 留空，禁止脚本执行；抽屉关闭立即 revoke Blob URL。
- **批量上传**：仅前端改造，`n-upload multiple` + 逐文件 `customRequest`；上传中显示计数，列表刷新 300ms 防抖，单文件失败不阻塞其它文件。
- **反馈 / 重生成 / 复制**：`MessageFeedback` 以 `(MessageId, UserId)` 唯一，同评分再次点击取消；重新生成删除最后一条助手消息并带 `Regenerate=true` 复用用户问题，后端不重复插入用户消息；回答记录 `LatencyMs`；复制走 Clipboard API，失败提示手动选择。**反馈映射用可空字典**：值类型字典的 `GetValueOrDefault` 会返回 `Like(0)`，未评价消息会误显示为已点赞（T9 实测修复）。
- **知识库设置**：切片长度 200–4000、重叠 0–`min(500, 长度/2)`、TopK 1–8、系统提示词可覆盖；只影响新上传文档与新提问，设置弹窗明确提示。
- **仪表盘**：按当前用户与其可访问知识库聚合；数字卡为文档/就绪/提问/平均耗时，趋势为最近 7 天**提问数**（不是助手回答数），无数据日期补 0 而不是报错。
- **知识库工作台**：资料库并入知识库页，左侧库列表、右侧标题/统计/设置/成员/文档区（`KnowledgeWorkspace` + `KnowledgeDocuments`）；问答页移除二级标签，会话侧栏固定，聊天头部「提问范围」与工作台共用 `documents.knowledgeBaseId`。
- **前端渲染**：助手回答用 `markdown-it`（`html:false`）渲染；用户消息保持纯文本；统一暖纸/墨绿视觉与可见焦点。
- **执行计划**：见 [`docs/plans/2026-10-24-knowvault-p4-mvp.md`](docs/plans/2026-10-24-knowvault-p4-mvp.md)

### 9.7 P5 混合检索与用量（2026-10-27）

- **执行计划**：见 [`docs/plans/2026-10-27-knowvault-p5-mvp.md`](docs/plans/2026-10-27-knowvault-p5-mvp.md)；需求细化见 [`docs/driver/需求/需求分析-1027-P5.md`](docs/driver/需求/需求分析-1027-P5.md)
- **默认开关**：混合检索默认开，查询改写默认关；均支持请求级覆盖（`HybridEnabled` / `RewriteEnabled`）
- **默认配置**：

| 配置键 | 默认值 |
|---|---|
| `Ai:HybridEnabled` | `true` |
| `Ai:Hybrid:DenseWeight` / `KeywordWeight` / `VariantWeight` | `1.0` / `1.0` / `0.8` |
| `Ai:Hybrid:RrfK` / `PerQueryCandidateCount` | `60` / `10` |
| `Ai:RerankEnabled` / `RerankCandidateCount` | `true` / `20`（沿用） |
| `Ai:TopK` / `HistoryMaxMessages` | `5` / `6`（沿用） |
| `Ai:QueryRewriteEnabled` | `false` |
| `Ai:QueryRewrite:MaxVariants` / `MinQuestionLength` / `MaxTokens` | `2` / `6` / `200` |
| `Ai:Pricing:DeepSeekInputPerMillion` / `DeepSeekOutputPerMillion` | `2` / `8` |
| `Ai:Pricing:EmbeddingPerMillion` / `RerankPerMillion` | `0.07` / `0.07` |

价格单位为 CNY / 百万 token，2026-10-27 按 DeepSeek 与 SiliconFlow 官网标价写入配置；费用为估算值，以账单为准

#### 9.7.1 检索流程

`提问 → 范围解析 →（可选）查询改写 → 批量 embedding → dense + sparse 双路召回 → 应用层加权 RRF → 重排（用原问题）→ TopK → 引用 → DeepSeek 流式生成`

- 单次问答只有一轮改写、一次批量 embedding、一次重排；多个查询共享同一候选池
- 融合：`Σ_query Σ_branch（权重 × 1/(RrfK + rank)）`，默认 `RrfK=60`、dense/keyword 各 `1.0`、改写变体 `0.8`、每查询每分支候选 10、融合后重排候选 20、TopK 5（上限 8）
- 改写：多查询变体（非 HyDE），一次非流式调用，原问题始终保留、最多 2 条变体；`temperature=0`、`max_tokens=200`；只带原问题与最近 2 条历史（去掉 `[n]` 引用标记）；短于 6 字不改写
- 纯向量快速路径：混合与改写都关闭时跳过 sparse 与 RRF，行为与 P4 一致
- 降级：sparse 查询失败按纯 dense 继续，改写失败按原问题继续；旁路失败不中断主流程
- 引用分：重排开启时为重排分，关闭时为 RRF 分；不跨问题比较

#### 9.7.2 中文切词与稀疏向量

- 切词：NFKC + 小写；CJK 连续片段取**重叠二元组**（单字保留）；拉丁/数字连续片段长度 ≥ 2；内置最小停用词
- 哈希：FNV-1a 32 位，映射到 `1..uint.MaxValue-1`，**禁止 `string.GetHashCode()`**（跨进程不稳定）
- 权重：文档侧 `1 + ln(tf)`，查询侧每个唯一词元 `1.0`
- 索引文本 = `HeadingPath + "\n" + Content`；payload 的 `content` 不变
- 代码：`src/KnowVault.Domain/Search/`（`KeywordTokenizer` / `KeywordStopWords` / `SparseVectorBuilder` / `SparseVectorData`）

#### 9.7.3 Qdrant schema 与重建方式

- 单 collection `knowvault_chunks`：**unnamed dense**（1024 维 Cosine，不变）+ **named sparse `text`**（`Modifier.Idf`）
- 稀疏向量不能通过 PATCH 追加到既有 collection，P4 → P5 必须重建，步骤：Admin 调用 `POST /api/app/maintenance/rebuild-vector-index` → 删除旧 collection 并按新 schema 重建 → 枚举全部文档走一次重新处理 → 返回提交数量，前端/接口按文档状态轮询；不自动迁移
- `EnsureCollectionAsync` 检测到缺少 `text` 时抛 `VectorSchemaMismatchException`，提示执行重建，不静默降级
- 服务端 weighted RRF 不可用（本机 Qdrant v1.15 实测 400），融合在应用层完成

#### 9.7.4 多库过滤与会话 scope

- 多库：`VectorFilter.KnowledgeBaseIds` 走 Qdrant `MatchAny`；`AllKnowledgeBases` 由服务端解析为当前用户可访问库集合
- 权限：逐库校验 Viewer，任一库越权即整体拒绝
- 用量归属：多库/全局提问的 `AppAiUsageRecords.KnowledgeBaseId` 为 null，仪表盘单列
- 会话：`Conversation.KnowledgeBaseScopeJson` + `ScopeAll` 保存提问范围，按主库归组；多库提问不使用单库自定义系统提示词
- 多库共用同一候选池、一次重排，不按库配额

#### 9.7.5 用量与成本口径

- `AppAiUsageRecords` 记录 Chat / Embedding / Rerank / QueryRewrite 的模型、输入/输出 token、耗时、估算费用；`AskId` 关联同一次问答，摄取 embedding 带 `DocumentId` 且 `AskId` 为 null
- 价格配置化（`Ai:Pricing:*`，CNY/百万 token），写入时固化 `EstimatedCostCny`；usage 缺失时按字符数估算并置 `IsEstimated=true`
- 记录走独立小 UoW，写失败只 warning，不阻断问答/摄取主流程
- 仪表盘新增用量区：今日/近 7 天 token 与费用、每日趋势、平均每次提问 token、按知识库（多库/全局单列），并明示是否存在本地估算值

#### 9.7.6 评估方法与三组模式

- 语料：项目自写 6 篇 RAG 主题 Markdown；金标 30 题（10 关键词 / 10 语义 / 5 跨文档 / 5 无答案）
- 三组模式：A 纯向量、B 混合、C 混合 + 改写；同一金标集、同一 TopK 与重排配置
- 指标：Recall@5、MRR@10、nDCG@5、CitationHitRate、CitationPrecision、NoAnswerRate、平均耗时、token、费用
- 工具：`tools/KnowVault.Eval` 控制台直连 `RagService` / `QdrantVectorStore`，报告写 `docs/eval/reports/*.md` + `*.json`
- 2026-10-27 结果：三组检索指标完全相同（Recall@5 0.900、MRR@10 0.847、nDCG@5 0.858），混合与改写未带来可测增益，也未回退；结论与限制见 [`docs/eval/reports/2026-10-27-p5-eval.md`](docs/eval/reports/2026-10-27-p5-eval.md)

## 10. P1 执行计划

- 详见 [`docs/plans/2026-10-05-knowvault-p1-mvp.md`](docs/plans/2026-10-05-knowvault-p1-mvp.md)：含冻结规格、14 个任务的文件级改动、完整代码与逐项验证命令、风险登记与完成定义。

## 11. P2 执行计划

- 详见 [`docs/plans/2026-10-14-knowvault-p2-mvp.md`](docs/plans/2026-10-14-knowvault-p2-mvp.md)：含冻结规格（六项决策 + 默认项 D1–D8）、8 个任务的文件级改动、15 项端到端验收、风险登记与完成定义。
- **状态：已完成（2026-10-14）**。逐项命令与实测输出写在该文件 §5「Task 8 —— 端到端验收」：`dotnet build KnowVault.slnx` 0 错误 0 警告、`dotnet test tests\KnowVault.Tests` 32/32、`dotnet format style` 无新增违规、47 个改动文件行尾全部合规（CRLF 无 BOM）、`npm run build` 通过。

## 12. P3 执行计划

- 详见 [`docs/plans/2026-10-18-knowvault-p3-mvp.md`](docs/plans/2026-10-18-knowvault-p3-mvp.md)：含冻结规格（五项决策）、T0–T7 文件级改动、12 项端到端验收、风险登记与完成定义。
- **状态：代码实现完成（2026-10-18）**：build 0 警告 0 错误、单测 33/33、集成测试 2/2、`npm run build` 通过；Seq trace / 权限 403 / 5 次重试进 error queue 需按该文件 §2 手工验收。

## 13. P4 执行计划

- 详见 [`docs/plans/2026-10-24-knowvault-p4-mvp.md`](docs/plans/2026-10-24-knowvault-p4-mvp.md)：含 D1–D13 决策、T1–T10 文件级改动、15 项端到端验收、风险登记与完成定义。
- **状态：已完成（2026-10-26）**：`dotnet build KnowVault.slnx` 0 警告 0 错误、单测 35/35、集成测试 3/3、`npm run build` 通过；15 项验收逐项实测，Aspire 一键启动因本机 DCP 不稳定沿用等价手工容器路径（P1 已记录）。
