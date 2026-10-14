# KnowVault P2「演示效果」实施计划

> **执行方式**：逐任务执行，每个任务以「构建/测试 + 提交」收尾；执行纪律见 `executing-plans` 技能。
> **需求规格已于 2026-10-14 冻结**（见 §0，用户逐项确认）。执行中若必须偏离规格，**先改 §0 再改代码**，禁止静默偏离。
> **动手前的铁律**（来自 `AGENTS.md`）：改文件前先 `read`；行尾整文件一致（新文件 CRLF、无 BOM、缩进用 Tab）；注释中文且 `<summary>` 多行展开、正文结尾不写句号；枚举名必须带 `Enum` 后缀且一文件一枚举；扁平根命名空间（`namespace KnowVault.Xxx`，块级声明）；异步方法一律带 `cancellationToken` 并逐层透传；单语句 `if` 去大括号且与 `if` 同行（链上有 `else` 则保持大括号分行）。
> **分支**：`feat/p2-mvp`（自 `main` 切出）。

**Goal:** 把 P1 的「能用」闭环升级为「能演示」——**逐字流式回答（SignalR）+ 重排带来的引用质量 + 多轮追问 + 知识库可视化增删改**，四项全部端到端真跑（DeepSeek + Qwen），并清掉 P1 遗留的 401 语义问题。

**Architecture:** 分层接缝保持不变：模型调用进 `KnowVault.Ai`，用例编排进 `KnowVault.Application`，传输层（Agent/Hub/Controller）只做搬砖。流式链路为 `QaHub`（HttpApi）→ `IQaStreamService`（Application）→ `IRagService.AnswerStreamAsync`（Ai）→ `ILlmChatService.CompleteStreamAsync`（Ai，SK 流式）。**「非流式 REST 问答」不再单独实现**，而是把同一份事件流拼成整段文本返回，保证两条入口行为一致。

**P2 新增依赖:**

| 用途 | 包 | 版本 | 许可 |
|---|---|---|---|
| 前端实时通信 | `@microsoft/signalr`（npm） | **^10.0.11** | MIT |
| SignalR 服务端 | （无） | — | .NET 10 共享框架 `Microsoft.AspNetCore.SignalR.Core` 自带，**不引任何 NuGet 包** |

---

## §0 冻结的需求规格（2026-10-14，用户确认）

### 0.1 六项决策

| # | 决策 |
|---|---|
| 1 | **多轮历史范围**：只做「Prompt 带最近 N 轮历史」；**不做**会话列表侧边栏 / 会话管理 API |
| 2 | **知识库删除边界**：**只允许删除空知识库**（零文档）；文档删除、Qdrant points 清理、MinIO 对象删除一律留 P3 |
| 3 | **工程收尾**：401 语义纳入 P2；「重新处理」入口与 RabbitMQ 持久化**待 Task 7 开工前再确认勾选** |
| 4 | **前端结构**：用 `n-tabs` 切「问答 / 知识库」两个视图，**不引入 vue-router** |
| 5 | **SignalR 协议**：Hub 方法返回 `IAsyncEnumerable<QaStreamEventDto>`（服务端流），前端用 `connection.stream(...).subscribe(...)`；**不用** `Clients.Caller.SendAsync` 事件推送 |
| 6 | **Rerank**：默认开启（`RerankEnabled = true`）；调用失败或返回空时 **fail-open** 退回向量分顺序，只在日志留 Warning |

### 0.2 默认项（用户未反对，按此执行）

- **D1** 重排模型 `qwen3-rerank`，走**经典域名** `https://dashscope.aliyuncs.com/api/v1`（已实测可用，无需工作空间域）；新增独立配置项 `Ai:QwenRerankBaseUrl`，**不复用** `QwenBaseUrl`（那是 OpenAI 兼容模式的 `/compatible-mode/v1`，重排不属于兼容模式）。
- **D2** 召回候选数 `RerankCandidateCount = 20`（Qdrant 先取 20 条 → 重排 → 取 topK ≤ 8）。
- **D3** 引用里 `Score` 的语义由「余弦相似度」变为「重排相关性分（0~1，仅请求内相对可比）」；前端展示方式不变，但文档与注释要说清楚。
- **D4** 历史条数 `Ai:HistoryMaxMessages = 6`（3 轮往返）；按 `CreationTime` 升序取最近 6 条，**读取必须发生在写入本轮提问之前**。
- **D5** 渲染历史时**剥掉助手旧回答里的 `[编号]` 引用标记**（旧编号与本轮资料编号冲突），并在 System prompt 增加第 5 条规则：历史只用于理解指代，事实依据必须来自本次资料片段。
- **D6** Hub 路由固定 `/signalr/qa`；Vite dev 通过 `/signalr` 代理（`ws: true`）转发，避免跨域与证书问题。
- **D7** 前端在 SignalR 连接失败时**降级回 REST 非流式提问**（`POST /api/app/qa/ask`），保证演示环境网络抖动时仍能出答案。
- **D8** 知识库致命错误（删默认库、删非空库、重名）沿用仓库既有做法：`UserFriendlyException` + 中文文案（与 `DocumentAppService` 一致），不新增错误码与本地化资源。

### 0.3 风险与已核实结论（本轮已实测）

- **R1 `gte-rerank` 已下线**（2026-10-14 实测，用 `.env` 的 `QWEN_API_KEY`）：
  - `POST https://dashscope.aliyuncs.com/api/v1/services/rerank/text-rerank/text-rerank` + `qwen3-rerank` → **HTTP 200**，`{"output":{"results":[{"index":0,"relevance_score":0.9114},{"index":1,"relevance_score":0.2282}]},"usage":{"total_tokens":60}}`；
  - 同端点 + `gte-rerank-v2` → HTTP 200；
  - 同端点 + **`gte-rerank`（P1 的默认值）→ HTTP 403 `{"code":"AccessDenied"}`**。
  - 结论：P1 的 `AiOptions.QwenRerankModel = "gte-rerank"` 是**死配置**，必须改成 `qwen3-rerank`，并把 `.env` / `.env.example` / AppHost 映射一并对齐。
  - 请求契约：`{"model","input":{"query","documents":[...]},"parameters":{"top_n","return_documents":false}}`；响应 `output.results[].{index,relevance_score}`；限制：文档数 ≤ 500、单条 ≤ 4000 token、单请求 ≤ 120000 token（我们只用 20 条，远离上限）。
- **R2 OpenIddict 不读 query string 令牌**：对 `openiddict.validation.aspnetcore 7.5.0` 的 DLL 做字符串扫描，`access_token` **命中 0 次**（社区同结论：openiddict-core #666、abpframework/abp #5239）。而 WebSocket 无法携带自定义请求头，SignalR JS 客户端只能把令牌放 query string → **必须在认证中间件之前把 `?access_token=` 搬运成 `Authorization: Bearer`**（只对 hub 路径生效）。
- **R3 ABP 提供挂 Hub 的正规入口**：`UseConfiguredEndpoints(IApplicationBuilder, Action<IEndpointRouteBuilder>)` 存在（本机 `volo.abp.aspnetcore 10.5.0` XML 文档核实，注释写明「internally uses the standard app.UseEndpoints(...)」）→ 把 Host 里最后的 `app.UseConfiguredEndpoints();` 换成带 `MapHub` 的重载即可，不要自己再写 `UseEndpoints`。
- **R4 流式无需新增后端依赖**：SK 1.80.0 的 `IChatCompletionService.GetStreamingChatMessageContentsAsync(ChatHistory, PromptExecutionSettings, Kernel, CancellationToken)` 返回 `IAsyncEnumerable<StreamingChatMessageContent>`，`.Content` 即增量文本；SignalR 服务端在 .NET 10 共享框架内（`Microsoft.AspNetCore.App.Ref\10.0.12\ref\net10.0\Microsoft.AspNetCore.SignalR.Core.dll` 实测存在）。
- **R5 `IAsyncEnumerable` 与 ABP `UnitOfWork` 拦截器天然冲突**：UoW 拦截器在**方法调用返回时**就结束工作单元，而 `async IAsyncEnumerable` 方法调用时只返回一个可枚举对象，真正的工作发生在枚举过程中 → 枚举期间的仓储调用会落到「无环境 UoW」上。**因此 `QaStreamService` 不依赖环境 UoW，每次落库都用 `_unitOfWorkManager.Begin(requiresNew: true)` 显式包一段**（并由 `[RemoteService(IsEnabled = false)]` 保证它不会被 ABP 生成动态 API 控制器）。
- **R6 Hub 里的 `CurrentUser` / `CurrentTenant` 可能拿不到**：WebSocket 上的 hub 调用不走逐次 HTTP 管道，ABP 的 `ICurrentPrincipalAccessor` 作用域可能为空。单租户下 `CurrentTenant.Id == null` → `RagRequest.TenantId = Guid.Empty`，恰好命中 P1 payload 的 null 归一化规则，检索不受影响；Task 5 落地时必须**用日志实测确认**，必要时在 Hub 方法里用 `Context.User` 推进 `ICurrentPrincipalAccessor.Change(...)`。
- **R7 `async IAsyncEnumerable` 的令牌参数需要 `[EnumeratorCancellation]`**：否则编译器告警 CS8425，且 `WithCancellation` 无法真正把取消传到 HTTP 调用。

### 0.4 非目标（P2 明确不做）

会话列表/侧边栏与会话管理 API、文档删除与 Qdrant points 清理、MinIO 孤儿对象清理、混合检索（BM25）、重排结果落库、多租户隔离强化、OCR、URL 抓取、OpenTelemetry/Seq 全链路、Testcontainers 集成测试、同名去重、Aspire/DCP 环境问题修复、Ingest 进度实时推送（前端仍 2s 轮询）。

### 0.5 测试策略（延续 P1）

只加 `tests/KnowVault.Tests` 的**纯函数 / 手写 fake 单测**，不引 Testcontainers、不连数据库与网络：

- 重排响应解析（`QwenRerankResponseParser`）：正常、缺 `index`、错误体（含 `code`）；
- 重排合并与降级（`RagService`，用 xunit 手写 fake 实现 `IEmbeddingService` / `IVectorStore` / `ILlmChatService` / `IRerankService`）：重排后顺序与分数被替换、重排抛异常时退回向量分；
- Prompt 组装（`RagPromptBuilder`）：**既有 4~6 条断言需随签名一起改**，新增历史渲染、历史引用标记剥离、无资料 + 有历史三类用例。

---

## §1 任务总览与依赖顺序

| Task | 内容 | 依赖 | 主要产出 |
|---|---|---|---|
| 0 | 文档与配置契约同步 | — | TECH_STACK/AGENTS/.env.example/AppHost 与本计划 |
| 1 | Rerank（Ai 层 + RagService 接入） | 0 | `IRerankService` / `QwenRerankService` / 解析器 / 单测 |
| 2 | 多轮会话历史 | 1 | `LlmChatMessage` / `RagPromptBuilder.Build(...history)` / 历史加载 |
| 3 | 流式生成（Ai 层） | 2 | `ILlmChatService.CompleteStreamAsync` / `RagStreamResult` / `AnswerStreamAsync` |
| 4 | 流式编排（Application） | 3 | `IQaStreamService` / `QaStreamService` / `QaAppService` 复用 |
| 5 | SignalR 传输 + 前端流式 | 4 | `QaHub` / 令牌中间件 / Host 装配 / `qaHub.js` / ChatPanel |
| 6 | 知识库管理（后端 + 前端） | 4 | KB CRUD API / `KnowledgeBasePanel.vue` / n-tabs 视图 |
| 7 | 工程收尾（401 语义等） | 5 | 401 修复（其余项开工前确认） |
| 8 | 端到端验收 + 文档回写 | 全部 | §2 验收记录 + TECH_STACK/AGENTS 回写 |

**每个任务的收尾动作固定为**：

```powershell
dotnet build KnowVault.slnx
dotnet test  tests\KnowVault.Tests\KnowVault.Tests.csproj
dotnet format style --verify-no-changes --diagnostics IDE1006 --project KnowVault.slnx
# 逐文件行尾自查（CRLF 与 loneLF 必须有一个为 0）
git -C . add -A; git -C . commit -m "<type>: <subject>"
```

---

## Task 0：文档与配置契约同步

**Files:**
- Modify: `TECH_STACK.md`（§6 路线图 P2 行 → 已实施；新增 §9.2 P2 定稿；新增 §11 P2 执行计划引用）
- Modify: `AGENTS.md`（§二 结构注释补 Hub/Ai 重排与流式；§七 待办补 P2 引用）
- Modify: `.env.example`（`QWEN_RERANK_MODEL=qwen3-rerank`、新增 `QWEN_RERANK_BASE_URL`）
- Modify: `aspire/KnowVault.AppHost`（`QWEN_RERANK_BASE_URL` → `Ai__QwenRerankBaseUrl`、`QWEN_RERANK_MODEL` → `Ai__QwenRerankModel` 的映射；按既有 `DEEPSEEK_*` 写法照抄）
- Create: `docs/plans/2026-10-14-knowvault-p2-mvp.md`（本文件）

**Step 1**：以上四份文档先改，再动代码（规格先行的既有约定）。

**Step 2**：`.env`（本地、不入库）由用户确认后自行补 `QWEN_RERANK_BASE_URL` / `QWEN_RERANK_MODEL`；Task 1 的默认值要保证**缺配置也能跑**。

**验收**：`git status` 只包含上述文件；`.env` 未进版本库。

---

## Task 1：Rerank（Ai 层 + RagService 接入）

**Files:**
- Create: `src/KnowVault.Ai/Rerank/RerankResult.cs`
- Create: `src/KnowVault.Ai/Rerank/IRerankService.cs`
- Create: `src/KnowVault.Ai/Rerank/QwenRerankResponseParser.cs`
- Create: `src/KnowVault.Ai/Rerank/QwenRerankService.cs`
- Modify: `src/KnowVault.Ai/Configuration/AiOptions.cs`
- Modify: `src/KnowVault.Ai/Rag/RagService.cs`
- Modify: `src/KnowVault.Ai/KnowVaultAiModule.cs`
- Modify: `src/KnowVault.HttpApi.Host/appsettings.json`（`Ai` 节补三个新键）
- Test: `tests/KnowVault.Tests/QwenRerankResponseParserTests.cs`、`tests/KnowVault.Tests/RagServiceRerankTests.cs`

**Step 1：配置项（`AiOptions`）**

```csharp
/// <summary>
/// 重排服务的基础地址，注意与 QwenBaseUrl 不同：重排不属于 OpenAI 兼容模式
/// </summary>
public string QwenRerankBaseUrl { get; set; } = "https://dashscope.aliyuncs.com/api/v1";

/// <summary>
/// 重排模型名，gte-rerank 已于 2026-05-30 下线，默认使用 qwen3-rerank
/// </summary>
public string QwenRerankModel { get; set; } = "qwen3-rerank";

/// <summary>
/// 是否启用重排（关闭时直接使用向量检索结果）
/// </summary>
public bool RerankEnabled { get; set; } = true;

/// <summary>
/// 重排前从向量库召回的候选条数，需不小于最终 topK
/// </summary>
public int RerankCandidateCount { get; set; } = 20;

/// <summary>
/// 带入提示词的历史消息条数上限（按时间取最近若干条）
/// </summary>
public int HistoryMaxMessages { get; set; } = 6;
```

> `HistoryMaxMessages` 提前放在本任务，Task 2 直接复用。

**Step 2：模型与接口（接口留在 Ai 层，不下沉 Domain）**

理由：`IEmbeddingService` 下沉 Domain 是因为 Ingest 需要它（P1 Task 8 记录）；重排只有 Ai 内部使用，放 Domain 反而制造无用耦合。

```csharp
namespace KnowVault.Ai
{
	/// <summary>
	/// 重排结果中的一条：指向输入 documents 的下标与请求内相对相关性分数
	/// </summary>
	public class RerankResult
	{
		/// <summary>
		/// 该结果对应输入 documents 的下标（从 0 开始）
		/// </summary>
		public int Index { get; set; }

		/// <summary>
		/// 请求内的相对相关性分数，取值 0~1，跨请求不可比
		/// </summary>
		public double Score { get; set; }
	}
}
```

```csharp
namespace KnowVault.Ai
{
	/// <summary>
	/// 文本重排服务抽象，把「向量召回」的候选按与问题的相关性重新排序
	/// </summary>
	public interface IRerankService
	{
		/// <summary>
		/// 对候选文档按与查询的相关性重排
		/// </summary>
		/// <param name="query">用户问题</param>
		/// <param name="documents">候选文档正文，顺序即返回结果中的下标语义</param>
		/// <param name="topN">返回条数上限</param>
		/// <param name="cancellationToken">取消令牌</param>
		/// <returns>按相关性降序的重排结果</returns>
		Task<IReadOnlyList<RerankResult>> RerankAsync(string query, IReadOnlyList<string> documents, int topN, CancellationToken cancellationToken = default);
	}
}
```

**Step 3：响应解析（纯函数，可测）**

```csharp
namespace KnowVault.Ai
{
	/// <summary>
	/// 解析 DashScope 文本重排接口的响应体
	/// </summary>
	public static class QwenRerankResponseParser
	{
		/// <summary>
		/// 解析重排响应，按 relevance_score 降序返回
		/// </summary>
		/// <param name="json">接口返回的 JSON 文本</param>
		/// <returns>重排结果，缺少必要字段的条目会被跳过</returns>
		/// <exception cref="System.Text.Json.JsonException">响应不是合法 JSON</exception>
		public static IReadOnlyList<RerankResult> Parse(string json)
		{
			using var document = JsonDocument.Parse(json);
			var results = new List<RerankResult>();
			if (!document.RootElement.TryGetProperty("output", out var output)) return results;
			if (!output.TryGetProperty("results", out var items) || items.ValueKind != JsonValueKind.Array) return results;
			foreach (var item in items.EnumerateArray())
			{
				if (!item.TryGetProperty("index", out var indexElement) || !indexElement.TryGetInt32(out var index)) continue;
				if (!item.TryGetProperty("relevance_score", out var scoreElement) || !scoreElement.TryGetDouble(out var score)) continue;
				results.Add(new RerankResult { Index = index, Score = score });
			}

			return results.OrderByDescending(result => result.Score).ToList();
		}
	}
}
```

**Step 4：Qwen 重排客户端（照 `QwenEmbeddingService` 的 `IHttpClientFactory` 模板）**

```csharp
namespace KnowVault.Ai
{
	/// <summary>
	/// 基于 DashScope 文本重排接口的重排实现
	/// </summary>
	[ExposeServices(typeof(IRerankService))]
	public class QwenRerankService : IRerankService, ITransientDependency
	{
		private const string RerankPath = "services/rerank/text-rerank/text-rerank";

		/// <summary>
		/// 构造函数
		/// </summary>
		/// <param name="httpClientFactory">HTTP 客户端工厂</param>
		/// <param name="options">AI 配置</param>
		/// <param name="logger">日志</param>
		public QwenRerankService(IHttpClientFactory httpClientFactory, IOptions<AiOptions> options, ILogger<QwenRerankService> logger) { ... }

		/// <inheritdoc />
		public async Task<IReadOnlyList<RerankResult>> RerankAsync(string query, IReadOnlyList<string> documents, int topN, CancellationToken cancellationToken = default)
		{
			if (documents.Count == 0) return [];
			var baseUrl = _options.QwenRerankBaseUrl.TrimEnd('/');
			var payload = new
			{
				model = _options.QwenRerankModel,
				input = new { query, documents },
				parameters = new { top_n = Math.Clamp(topN, 1, documents.Count), return_documents = false }
			};
			using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/{RerankPath}");
			request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.QwenApiKey);
			request.Content = JsonContent.Create(payload);
			using var response = await _httpClientFactory.CreateClient().SendAsync(request, cancellationToken);
			var body = await response.Content.ReadAsStringAsync(cancellationToken);
			if (!response.IsSuccessStatusCode) throw new AiCallException($"Qwen rerank 调用失败：HTTP {(int)response.StatusCode} {body}");
			// DashScope 的鉴权/额度错误可能以 200 + code 字段返回，必须显式拦下
			var results = QwenRerankResponseParser.Parse(body);
			if (results.Count == 0 && body.Contains("\"code\"")) throw new AiCallException($"Qwen rerank 返回错误：{body}");
			return results;
		}
	}
}
```

**Step 5：注册（显式注册，避免 P1 踩过的约定坑）**

```csharp
context.Services.AddTransient<IRerankService, QwenRerankService>();
```

**Step 6：RagService 接入重排**

```csharp
var topK = Math.Clamp(request.TopK <= 0 ? _options.TopK : request.TopK, 1, 8);
// 重排需要「宽召回 + 精排」，召回数不小于 topK
var candidates = _options.RerankEnabled ? Math.Clamp(_options.RerankCandidateCount, topK, 100) : topK;
var hits = await _vectorStore.SearchAsync(vectors[0], filter, candidates, cancellationToken);
var chunks = hits.Select(MapChunk).OrderByDescending(chunk => chunk.Score).ToList();
chunks = await RerankAsync(request.Question, chunks, topK, cancellationToken);
chunks = chunks.Take(topK).ToList();
```

```csharp
/// <summary>
/// 调重排服务重排候选切片，失败时退回向量检索顺序（fail-open）
/// </summary>
private async Task<List<RagRetrievedChunk>> RerankAsync(string question, List<RagRetrievedChunk> chunks, int topK, CancellationToken cancellationToken)
{
	if (!_options.RerankEnabled || chunks.Count <= 1) return chunks;
	try
	{
		var documents = chunks.Select(chunk => chunk.Content).ToList();
		var results = await _rerankService.RerankAsync(question, documents, topK, cancellationToken);
		if (results.Count == 0) return chunks;
		// 重排分替换向量分：它才是本轮引用的真实相关性依据
		return results.Where(result => result.Index >= 0 && result.Index < chunks.Count)
			.Select(result => chunks[result.Index].WithScore(result.Score))
			.ToList();
	}
	catch (Exception ex) when (ex is not OperationCanceledException)
	{
		_logger.LogWarning(ex, "重排失败，退回向量检索顺序");
		return chunks;
	}
}
```

> `RagRetrievedChunk` 需要加一个 `WithScore(double)`（或直接对象初始化器构造副本）；优先加方法以便复用。

**Step 7：测试（先红后绿）**

- `QwenRerankResponseParserTests`：正常响应（断言降序与下标映射）、缺 `index` 的条目被跳过、`output` 缺失返回空、空数组；
- `RagServiceRerankTests`：手写 fake `IRerankService` 返回颠倒的下标 → 断言 `Citations` 顺序与 `Score` 被替换；fake 抛 `AiCallException` → 断言退回向量分顺序且不抛；`RerankEnabled=false` → 不调用重排（fake 记录调用次数为 0）。

**验收**：单测全绿；`appsettings.json` 的 `Ai` 节含 `QwenRerankBaseUrl` / `QwenRerankModel=qwen3-rerank` / `RerankCandidateCount`；真实提问时日志出现重排调用。

---

## Task 2：多轮会话历史

**Files:**
- Create: `src/KnowVault.Ai/Chat/LlmChatMessage.cs`
- Modify: `src/KnowVault.Ai/Rag/RagModels.cs`（`RagRequest` 增加 `History`）
- Modify: `src/KnowVault.Ai/Rag/RagPromptBuilder.cs`（签名与历史渲染）
- Modify: `src/KnowVault.Ai/Rag/RagPromptTemplates.cs`（System prompt 增第 5 条规则）
- Modify: `src/KnowVault.Application/Qa/QaAppService.cs`（读取历史并传入）
- Test: `tests/KnowVault.Tests/RagPromptBuilderTests.cs`（改既有断言 + 新增用例）

**Step 1：消息模型（放 Ai 层，复用 Domain.Shared 的角色枚举）**

```csharp
namespace KnowVault.Ai
{
	/// <summary>
	/// 交给大模型的一轮对话消息
	/// </summary>
	public class LlmChatMessage
	{
		/// <summary>
		/// 角色（用户或助手）
		/// </summary>
		public MessageRoleEnum Role { get; set; }

		/// <summary>
		/// 消息正文
		/// </summary>
		public string Content { get; set; } = string.Empty;
	}
}
```

**Step 2：Prompt 组装改成「多轮消息」而不是把历史塞进一段用户文本**

```csharp
/// <summary>
/// 组装提示词：历史以独立消息存在，当前问题与资料片段作为最后一条用户消息
/// </summary>
/// <param name="question">本轮用户问题</param>
/// <param name="chunks">本轮检索到的片段（已按相关性降序）</param>
/// <param name="history">历史轮次（时间升序，可为空）</param>
/// <returns>system 提示词与完整消息列表</returns>
public static (string System, IReadOnlyList<LlmChatMessage> Messages) Build(string question, IReadOnlyList<RagRetrievedChunk> chunks, IReadOnlyList<LlmChatMessage>? history = null)
```

要点：

1. 先按原顺序放入 `history`（助手内容经 `StripCitationMarkers` 处理：正则 `\[\d+\]` 去掉并 `Trim`，空内容整条丢弃）；
2. 最后追加一条 `MessageRoleEnum.User`，正文与 P1 完全一致（`资料片段：` 段落 + `用户问题：{question}`），保证引用编号语义不变。

**Step 3：System prompt 增一条**

```text
5. 「对话历史」只用于理解指代（如「它」「这个」指什么），事实依据必须来自本次的「资料片段」；历史中的编号已失效，不要引用。
```

**Step 4：历史加载（Application 层，必须在写入本轮提问之前）**

```csharp
var messages = await _messageRepository.GetListAsync(message => message.ConversationId == conversationId, cancellationToken: cancellationToken);
var history = messages.OrderByDescending(message => message.CreationTime)
	.Take(_aiOptions.Value.HistoryMaxMessages)
	.OrderBy(message => message.CreationTime)
	.Select(message => new LlmChatMessage { Role = message.Role, Content = message.Content })
	.ToList();
```

> 会话消息量小，取全量后在内存排序；P3 若消息量上来再下推到 SQL。**顺序铁律：先读历史，再插本轮用户消息。**

**Step 5：测试**

- 既有断言跟着新签名改（`(system, user)` → `(system, messages)`，断言 `messages[^1].Content`）；
- 新增：历史按时间顺序出现在最后一条之前；助手旧回答里的 `[1]` 被剥离；空历史时行为与 P1 一致（回归保护）。

**验收**：单测全绿；端到端「它/这个」指代追问能答对（Task 8 验收项）。

---

## Task 3：流式生成（Ai 层）

**Files:**
- Modify: `src/KnowVault.Ai/Chat/ILlmChatService.cs`
- Modify: `src/KnowVault.Ai/Chat/DeepSeekChatService.cs`
- Create: `src/KnowVault.Ai/Rag/RagStreamResult.cs`
- Modify: `src/KnowVault.Ai/Rag/RagService.cs`（`AnswerStreamAsync` + `AnswerAsync` 改为拼流）

**Step 1：接口（**删除**一次性 `CompleteAsync`，只留流式，避免两条实现路径分叉）**

```csharp
/// <summary>
/// 以流式方式补全回答，逐段返回增量文本
/// </summary>
/// <param name="systemPrompt">系统提示词</param>
/// <param name="messages">完整对话消息（含历史与最后一条用户消息）</param>
/// <param name="cancellationToken">取消令牌</param>
/// <returns>增量文本流</returns>
IAsyncEnumerable<string> CompleteStreamAsync(string systemPrompt, IReadOnlyList<LlmChatMessage> messages, CancellationToken cancellationToken = default);
```

**Step 2：SK 流式实现（注意 `[EnumeratorCancellation]`，否则 CS8425 且取消传不下去）**

```csharp
public async IAsyncEnumerable<string> CompleteStreamAsync(string systemPrompt, IReadOnlyList<LlmChatMessage> messages, [EnumeratorCancellation] CancellationToken cancellationToken = default)
{
	var chat = _kernel.GetRequiredService<IChatCompletionService>();
	var history = new ChatHistory();
	history.AddSystemMessage(systemPrompt);
	foreach (var message in messages)
	{
		history.AddMessage(message.Role == MessageRoleEnum.User ? AuthorRole.User : AuthorRole.Assistant, message.Content);
	}

	var stream = chat.GetStreamingChatMessageContentsAsync(history, cancellationToken: cancellationToken);
	await foreach (var item in stream.WithCancellation(cancellationToken))
	{
		// SK 会给出空白的首帧/尾帧，直接丢弃，避免前端出现空跳动
		if (!string.IsNullOrEmpty(item.Content)) yield return item.Content;
	}
}
```

**Step 3：检索结果 + 增量流的结果对象**

```csharp
namespace KnowVault.Ai
{
	/// <summary>
	/// 流式问答的第一步结果：先给出引用切片，再给出增量文本流
	/// </summary>
	public class RagStreamResult
	{
		/// <summary>
		/// 检索（并按需重排）后的引用切片，顺序即引用编号顺序
		/// </summary>
		public required IReadOnlyList<RagRetrievedChunk> Chunks { get; init; }

		/// <summary>
		/// 大模型生成的增量文本流
		/// </summary>
		public required IAsyncEnumerable<string> Deltas { get; init; }
	}
}
```

**Step 4：`RagService` 双入口，单一实现**

- `AnswerStreamAsync(RagRequest, CancellationToken)`：检索 → 重排 → `RagPromptBuilder.Build` → 返回 `RagStreamResult`；
- `AnswerAsync(RagRequest, CancellationToken)`：`await AnswerStreamAsync(...)` 后把 `Deltas` 拼成整段文本，`Trim()` 后若非空则返回 `RagAnswer`，为空抛 `AiCallException("DeepSeek 返回了空回答。")`（**保持 P1 行为**）；
- 空回答检查放在包住 `Deltas` 的私有迭代器里，使流式与非流式两条路径都得到同样的保护：

```csharp
private async IAsyncEnumerable<string> StreamWithEmptyCheckAsync(string systemPrompt, IReadOnlyList<LlmChatMessage> messages, [EnumeratorCancellation] CancellationToken cancellationToken)
{
	var totalLength = 0;
	await foreach (var delta in _chatService.CompleteStreamAsync(systemPrompt, messages, cancellationToken).WithCancellation(cancellationToken))
	{
		totalLength += delta.Length;
		yield return delta;
	}

	if (totalLength == 0) throw new AiCallException("DeepSeek 返回了空回答。");
}
```

**验收**：单测全绿（既有 RagService 相关用例若因签名变化而失败，按新契约更新）；`dotnet build` 无 CS8425。

---

## Task 4：流式编排（Application 层）

**Files:**
- Create: `src/KnowVault.Application.Contracts/Qa/IQaStreamService.cs`
- Create: `src/KnowVault.Application.Contracts/Models/Qa/QaStreamEventDto.cs`
- Create: `src/KnowVault.Application.Contracts/Models/Qa/QaStreamEventKindEnum.cs`
- Create: `src/KnowVault.Application/Qa/QaStreamService.cs`
- Modify: `src/KnowVault.Application/Qa/QaAppService.cs`（`AskAsync` 改为消费事件流并拼接）
- Modify: `src/KnowVault.Application.Contracts/Models/Qa/AskQuestionInput.cs`（无需改动，Hub 复用同一 DTO）

**Step 1：事件协议（放 Contracts，因为 HttpApi 的 Hub 要消费它）**

```csharp
namespace KnowVault.Application.Contracts
{
	/// <summary>
	/// 流式问答事件类型
	/// </summary>
	public enum QaStreamEventKindEnum
	{
		/// <summary>
		/// 首帧：会话已就绪且检索/重排完成，携带会话 ID 与引用
		/// </summary>
		Started = 0,

		/// <summary>
		/// 增量回答文本
		/// </summary>
		Delta = 1,

		/// <summary>
		/// 末帧：回答已生成并落库
		/// </summary>
		Completed = 2
	}
}
```

```csharp
namespace KnowVault.Application.Contracts
{
	/// <summary>
	/// 流式问答的一帧事件
	/// </summary>
	public class QaStreamEventDto
	{
		/// <summary>
		/// 事件类型
		/// </summary>
		public QaStreamEventKindEnum Kind { get; set; }

		/// <summary>
		/// 会话 ID，仅首帧携带
		/// </summary>
		public Guid? ConversationId { get; set; }

		/// <summary>
		/// 增量文本，仅 Delta 帧携带
		/// </summary>
		public string? Delta { get; set; }

		/// <summary>
		/// 引用列表，仅首帧携带（检索阶段已确定）
		/// </summary>
		public IReadOnlyList<RagCitationDto> Citations { get; set; } = [];
	}
}
```

**Step 2：编排服务**

```csharp
namespace KnowVault.Application.Contracts
{
	/// <summary>
	/// 流式问答编排：负责会话与消息落库，并把问答过程以事件流返回
	/// </summary>
	public interface IQaStreamService
	{
		/// <summary>
		/// 流式提问：先落用户消息与历史，再检索、生成、落助手消息
		/// </summary>
		/// <param name="input">提问参数</param>
		/// <param name="cancellationToken">取消令牌（SignalR 传 Context.ConnectionAborted）</param>
		/// <returns>问答事件流</returns>
		IAsyncEnumerable<QaStreamEventDto> AskStreamAsync(AskQuestionInput input, CancellationToken cancellationToken = default);
	}
}
```

实现要点（`[RemoteService(IsEnabled = false)]` 防动态 API，显式 UoW 见 R5）：

1. 会话：`input.ConversationId` 有值则 `GetAsync`，否则新建（标题取问题前 128 字符）；
2. **先读历史**（Task 2 Step 4 的逻辑搬到这里），再在 `Begin(requiresNew: true)` 的 UoW 内插入用户消息并 `CompleteAsync`；
3. `await _ragService.AnswerStreamAsync(new RagRequest { KnowledgeBaseId = input.KnowledgeBaseId ?? KnowVaultConsts.DefaultKnowledgeBaseId, TenantId = CurrentTenant.Id ?? Guid.Empty, ConversationId = conversation.Id, Question = input.Question, TopK = input.TopK ?? 0, History = history }, cancellationToken)`；
4. `yield return Started`（带 `ConversationId` 与 `Citations`）；
5. 逐条转发 `Delta`（同时用 `StringBuilder` 累积全文）；
6. 生成结束后，在**新的** `Begin(requiresNew: true)` UoW 内插入助手消息（`CitationsJson = JsonSerializer.Serialize(citations)`），然后 `yield return Completed`；
7. 客户端中途断开 → `cancellationToken` 触发 → 迭代器退出，**不落助手消息**（半截回答不入库，这是刻意的）。

**Step 3：`QaAppService.AskAsync` 复用同一条路径**

`AskAsync` 改为消费 `_qaStreamService.AskStreamAsync(...)`：累积 `Delta`、取首帧的 `ConversationId` 与 `Citations`，返回原来的 `QaAnswerDto`。这样 REST 与 SignalR 的落库、历史、检索行为**只有一份实现**。

**验收**：`POST /api/app/qa/ask` 行为与 P1 一致（Swagger 手测 + 前端降级路径）；多轮追问能带上历史。

---

## Task 5：SignalR 传输 + 前端流式

**Files:**
- Create: `src/KnowVault.HttpApi/Hubs/QaHub.cs`
- Create: `src/KnowVault.HttpApi.Host/Middleware/SignalRAccessTokenMiddleware.cs`
- Modify: `src/KnowVault.HttpApi.Host/KnowVaultHttpApiHostModule.cs`（`AddSignalR`、中间件顺序、`MapHub`）
- Modify: `frontend/package.json`（`@microsoft/signalr`）
- Modify: `frontend/vite.config.js`（`/signalr` 代理，`ws: true`）
- Create: `frontend/src/api/qaHub.js`
- Modify: `frontend/src/api/http.js`（导出取 token 的方法，不改既有行为）
- Modify: `frontend/src/stores/chat.js`（流式状态机 + 停止 + REST 降级）
- Modify: `frontend/src/components/ChatPanel.vue`（逐字渲染 + 停止按钮）

**Step 1：Hub（HttpApi 层；服务契约在 Contracts，故无需引用 Application）**

```csharp
namespace KnowVault.HttpApi
{
	/// <summary>
	/// 流式问答 Hub：把应用层的问答事件流原样推给调用方
	/// </summary>
	/// <remarks>
	/// Hub 方法不能声明 CancellationToken（SignalR 会把它当成客户端参数），
	/// 因此取消令牌取自 Context.ConnectionAborted 并显式传给下游
	/// </remarks>
	[Authorize]
	public class QaHub : Hub
	{
		/// <summary>
		/// Hub 路由，供 Host 映射与令牌中间件共用
		/// </summary>
		public const string Route = "/signalr/qa";

		/// <summary>
		/// 构造函数
		/// </summary>
		/// <param name="qaStreamService">流式问答编排服务</param>
		public QaHub(IQaStreamService qaStreamService) => _qaStreamService = qaStreamService;

		/// <summary>
		/// 以服务端流的方式回答问题
		/// </summary>
		/// <param name="input">提问参数</param>
		/// <returns>问答事件流</returns>
		[HubMethodName("Ask")]
		public IAsyncEnumerable<QaStreamEventDto> AskAsync(AskQuestionInput input) => _qaStreamService.AskStreamAsync(input, Context.ConnectionAborted);
	}
}
```

> 若 `KnowVault.HttpApi` 编译不到 `Hub` 类型，在 csproj 补 `<FrameworkReference Include="Microsoft.AspNetCore.App" />`（正常情况下由 `Volo.Abp.AspNetCore.Mvc` 传递引入）。

**Step 2：令牌搬运中间件（R2 的直接对策）**

```csharp
namespace KnowVault.HttpApi.Host
{
	/// <summary>
	/// 把 SignalR 握手请求 query string 里的 access_token 提升为 Authorization 头
	/// </summary>
	/// <remarks>
	/// WebSocket 无法携带自定义请求头，SignalR 的 JS 客户端只能把令牌放在 query string；
	/// 而 OpenIddict 的验证处理不读取 query string，因此必须在认证中间件之前补一次搬运。
	/// 只对 hub 路径生效，避免把任意接口的令牌暴露面扩大到 URL 上
	/// </remarks>
	public class SignalRAccessTokenMiddleware
	{
		private const string AccessTokenKey = "access_token";

		/// <summary>
		/// 处理请求：命中 hub 路径且无 Authorization 头时，用 query string 的令牌补上
		/// </summary>
		/// <param name="context">HTTP 上下文</param>
		public async Task InvokeAsync(HttpContext context)
		{
			var isHubRequest = context.Request.Path.StartsWithSegments(QaHub.Route);
			var hasAuthorization = !string.IsNullOrEmpty(context.Request.Headers.Authorization);
			if (isHubRequest && !hasAuthorization)
			{
				var token = context.Request.Query[AccessTokenKey].ToString();
				if (!string.IsNullOrEmpty(token)) context.Request.Headers.Authorization = $"Bearer {token}";
			}

			await _next(context);
		}
	}
}
```

**Step 3：Host 装配（三处改动，顺序是关键）**

1. `ConfigureServices`：`context.Services.AddSignalR();`
2. `OnApplicationInitialization`：在 `app.UseAuthentication();` **之前**插 `app.UseMiddleware<SignalRAccessTokenMiddleware>();`
3. 最后一行 `app.UseConfiguredEndpoints();` → `app.UseConfiguredEndpoints(endpoints => endpoints.MapHub<QaHub>(QaHub.Route));`（R3）

**Step 4：前端客户端**

```javascript
// frontend/src/api/qaHub.js 要点
const connection = new HubConnectionBuilder()
  .withUrl('/signalr/qa', { accessTokenFactory: () => getToken() ?? '' })
  .withAutomaticReconnect()
  .configureLogging(LogLevel.Warning)
  .build()

// 服务端流：返回 subscription，调用方可 dispose() 取消
export function askStream(input, { onEvent, onError, onComplete }) {
  const subscription = connection.stream('Ask', input).subscribe({
    next: onEvent,
    error: onError,
    complete: onComplete,
  })
  return subscription
}
```

`chat.js` 状态机：`ask()` 先塞用户消息与助手占位（`streaming: true`），`Started` 帧写入 `conversationId` 与 `citations`，`Delta` 帧追加正文，`Completed`/`complete` 收尾；`stop()` 调 `subscription.dispose()`；`connection.start()` 失败时降级调 `qaApi.ask`（D7）。

**Step 5：前端逐字渲染**

`ChatPanel.vue`：助手消息正文为空且 `streaming` 时显示「正在生成…」；正文使用纯文本渲染（**不引 markdown 渲染器**，避免新增依赖与 XSS 面）；`streaming` 期间显示「停止生成」按钮；引用标签沿用现有 `n-tag`。

**验收**：浏览器逐字增长；「停止生成」立即停止且**不产生半截助手消息**（查库确认）；断开前端不报错；`npm run build` 通过。

---

## Task 6：知识库管理（后端 + 前端）

**Files:**
- Modify: `src/KnowVault.Application.Contracts/Interfaces/IKnowledgeBaseAppService.cs`
- Create: `src/KnowVault.Application.Contracts/Models/KnowledgeBases/CreateKnowledgeBaseInput.cs`
- Create: `src/KnowVault.Application.Contracts/Models/KnowledgeBases/UpdateKnowledgeBaseInput.cs`
- Modify: `src/KnowVault.Application/KnowledgeBases/KnowledgeBaseAppService.cs`
- Modify: `frontend/src/api/knowledgeBases.js`
- Create: `frontend/src/components/KnowledgeBasePanel.vue`
- Modify: `frontend/src/App.vue`（`n-tabs` 切「问答 / 知识库」）
- Modify: `frontend/src/stores/documents.js`（或新增 store action，承接 KB 增删改后的刷新）

**Step 1：契约**

```csharp
Task<KnowledgeBaseDto> CreateAsync(CreateKnowledgeBaseInput input, CancellationToken cancellationToken);
Task<KnowledgeBaseDto> UpdateAsync(UpdateKnowledgeBaseInput input, CancellationToken cancellationToken);
Task DeleteAsync(Guid id, CancellationToken cancellationToken);
```

`CreateKnowledgeBaseInput`：`[Required][StringLength(128)] string Name`、`[StringLength(512)] string? Description`；
`UpdateKnowledgeBaseInput`：`Guid Id` + 同上两个字段。

**Step 2：实现规则（D8，全部用 `UserFriendlyException` + 中文文案）**

1. `CreateAsync`：`new KnowledgeBase(GuidGenerator.Create(), input.Name.Trim(), input.Description?.Trim(), CurrentTenant.Id)`，`InsertAsync(autoSave: true)`；
2. `UpdateAsync`：`GetAsync(id)` → `entity.Update(name, description)` → `UpdateAsync(autoSave: true)`；
3. `DeleteAsync`：`id == KnowVaultConsts.DefaultKnowledgeBaseId` → 抛「默认知识库不可删除」；该库下 `Document` 数 > 0 → 抛「知识库下仍有文档，请先清空（文档删除在 P3 提供）」；否则 `DeleteAsync(autoSave: true)`；
4. 顺带对齐 `AGENTS.md` 的 CancellationToken 规范（这两个既有方法签名本来缺令牌，本次改接口必须同步补齐实现）。

**Step 3：前端界面**

`KnowledgeBasePanel.vue`：`n-data-table` 或 `n-list` 展示名称/描述/就绪文档数；「新建」用 `n-modal` + `n-form`；「编辑」复用同一表单；「删除」用 `n-popconfirm`，默认知识库的删除按钮禁用并提示；操作成功后刷新列表与文档面板选中的知识库。`App.vue` 用 `n-tabs` 切两个视图，「新会话」按钮仍留在 header。

**验收**：Swagger 可增改删；删默认库/非空库返回可读中文错误；前端操作后下拉与列表同步；空库可删。

---

## Task 7：工程收尾（开工前需再次确认勾选范围）

**已定范围：401 语义。**

- **现象**（P1 记录）：匿名或过期令牌访问 API 返回 **302** 而非 401，前端 axios 拦截器因此拿不到 401，无法自动登出。
- **定位步骤**：`curl -i /api/app/knowledge-base` 观察 `Location` 与 `WWW-Authenticate`；确认是认证 challenge 走了重定向（而非异常处理）。
- **候选修法**（定位后择优，改动前先在此登记）：给 API 路径的认证方案设置不重定向的 `Events.OnRedirectToLogin`（返回 401）；或在 Host 里为 `/api` 前缀配置统一的 challenge 行为。
- **待确认勾选项**：① 「重新处理」入口（`DocumentAppService.ReprocessAsync` + 按 documentId 清理旧向量 → **会引入 Qdrant 删除能力，与决策 2 的边界有交叉**）；② RabbitMQ 消息持久化与重投。

**验收**：匿名/过期令牌请求 API 得到 401 + `WWW-Authenticate`；前端过期后自动回到登录态。

---

## Task 8：端到端验收与文档回写

**Files:**
- Modify: `TECH_STACK.md`（§9.2 定稿核对、§6 路线图状态）
- Modify: `AGENTS.md`（§二 结构、§七 待办）
- Modify: 本文件（§5 执行记录）

**验收记录要求**：逐项写明「命令 + 实际输出/观察」，禁止只写结论。

---

## §2 端到端验收清单（Task 8 逐项打勾并记录证据）

1. 匿名访问 `/api/app/knowledge-base` → **401**（Task 7）；
2. 登录后上传一份 PDF/Markdown → 轮询到「已就绪」；
3. 提问 → 浏览器**逐字**出现回答，引用标签在看板出现且可点开定位到文档与片段序号；
4. 点「停止生成」→ 前端立即停止，库里**没有**半截助手消息；
5. 连续追问（第二轮用「它/这个」指代第一轮的名词）→ 回答正确，说明历史进入了提示词；
6. 重排生效证据：同一次提问的日志里出现重排调用，且引用顺序与向量分顺序不同（构造一个向量分高但实际不相关的片段作为反例）；
7. `RerankEnabled=false` 时同一提问仍能回答（开关有效）；
8. 知识库新建 / 改名 / 描述修改生效；删除空库成功；删除默认库、删除非空库被拒绝并提示中文原因；
9. `POST /api/app/qa/ask`（Swagger）仍返回完整答案与引用（REST 路径不被流式改造破坏）；
10. 前端在 SignalR 连不上时（临时改错端口）自动降级为 REST 一次性回答；
11. `dotnet build KnowVault.slnx` 0 error；
12. `dotnet test tests/KnowVault.Tests` 全绿（P1 19 条 + P2 新增）；
13. `dotnet format style --verify-no-changes --diagnostics IDE1006` 无输出；
14. 新增/改动文件行尾自查全部合规（每文件 `CRLF 与 loneLF 必有一个为 0`），无 BOM；
15. `cd frontend && npm run build` 成功。

---

## §3 风险登记（执行时逐条盯着）

| # | 风险 | 触发条件 | 应对 |
|---|---|---|---|
| 1 | `gte-rerank` 已下线 | 沿用 P1 默认配置 | 已实测 403；Task 0/1 全链路改成 `qwen3-rerank` + 经典域名 |
| 2 | SignalR 令牌传递失败（401 握手失败） | OpenIddict 不读 query string | 认证前搬运令牌（Task 5）；失败时前端降级 REST |
| 3 | `IAsyncEnumerable` + UoW 拦截器 | 流式编排里用环境 UoW | 显式 `Begin(requiresNew: true)`（R5） |
| 4 | Hub 里拿不到 CurrentUser/CurrentTenant | WebSocket 不走逐次 HTTP 管道 | 单租户下 `Guid.Empty` 可命中；实测确认，必要时推进 `ICurrentPrincipalAccessor` |
| 5 | CS8425（缺 `[EnumeratorCancellation]`） | 写 `async IAsyncEnumerable` | 所有流式方法显式标注该特性 |
| 6 | 前端正文含 Markdown/HTML | 模型输出带 `**` 或标签 | 纯文本渲染，不引渲染器 |
| 7 | 历史把助手旧答案当事实 | 多轮问答事实漂移 | System prompt 第 5 条 + 剥离历史引用标记 |
| 8 | 引用分语义变化引起误读 | 前端仍写「相似度」 | 文档、注释、界面文案统一改成「相关性」 |
| 9 | Vite 代理 WebSocket 未开 | 只加了 `target` 没加 `ws: true` | Task 5 显式配 `ws: true` |
| 10 | 半截回答入库 | 客户端断开后仍继续写库 | 迭代器被取消即不落助手消息 |
| 11 | 混合行尾 | 局部编辑 CRLF 文件 | 每任务收尾做逐文件行尾自查 |

---

## §4 完成定义（DoD）

- §2 的 15 项验收全绿，且负向用例（删默认库、删非空库、重排不可用、SignalR 不可用）表现符合预期；
- `dotnet build` 0 error、`dotnet test` 全绿、`IDE1006` 无告警、行尾合规；
- `TECH_STACK.md` 与 `AGENTS.md` 已按 Task 0/8 回写且与实际实现一致；
- 每个 Task 一个 Conventional Commits 提交，历史可读；
- 分支 `feat/p2-mvp`，做完由用户确认后合并。

---

## §5 执行记录（实测偏差，边做边记）

> 原则：**代码以仓库实际实现为准，本节记录计划与实现的差异及原因**，避免后来者照抄计划里已过时的代码。

### Task 0/1 —— 重排接口实测（2026-10-14）

- 经典域名 `https://dashscope.aliyuncs.com/api/v1` 仍可用于 rerank，**不需要**文档里给的工作空间域；
- `qwen3-rerank` 200、`gte-rerank-v2` 200、`gte-rerank` **403 AccessDenied**（已下线）；
- 响应除 `output.results` 外还有 `request_id` / `usage`；`results` 当前已按分数降序返回（客户端仍自行排序，不依赖服务端顺序）

### Task 1 —— Rerank 落地（2026-10-14）

- 新增 `src/KnowVault.Ai/Rerank/{RerankResult,IRerankService,QwenRerankResponseParser,QwenRerankService}.cs`；改动 `src/KnowVault.Ai/Configuration/AiOptions.cs`（+`QwenRerankBaseUrl`、默认模型改 `qwen3-rerank`、+`RerankEnabled` / `RerankCandidateCount` / `HistoryMaxMessages`）、`src/KnowVault.Ai/Rag/RagRetrievedChunk.cs`（+`WithScore`）、`src/KnowVault.Ai/Rag/RagService.cs`（宽召回 + 重排 + `Take(topK)` + fail-open）、`src/KnowVault.HttpApi.Host/appsettings.json`（Ai 节同步 5 个键）
- **不需要**改 `src/KnowVault.Ai/KnowVaultAiModule.cs`：ABP 约定注册按「接口名去掉首字母 I 后是否为类名后缀」匹配，`QwenRerankService` 以 `RerankService` 结尾即命中 `IRerankService`（P1 的 `QwenEmbeddingService` → `IEmbeddingService` 同理，模块里从来没有显式 `AddTransient`）；类上仍加 `[ExposeServices(typeof(IRerankService))]` 自我声明
- 重排失败用 `catch (Exception ex) when (ex is not OperationCanceledException)` → 记 warning 后退回向量分顺序（取消仍照常向上传播）
- 新增测试 `tests/KnowVault.Tests/QwenRerankResponseParserTests.cs`（4 条）、`tests/KnowVault.Tests/RagServiceRerankTests.cs`（4 条，手写 fake 替身）；`dotnet test` **27/27 通过**（P1 19 + 新增 8）
- ⚠️ **IDE1006 检查命令的现实偏差**：`dotnet format style KnowVault.slnx --verify-no-changes --diagnostics IDE1006` 的 `--diagnostics` 不生效，会连带报出既有文件的 `error IMPORTS: 修复导入顺序。` —— `src/KnowVault.Application.Contracts/KnowVaultApplicationContractsModule.cs(1,1)`、`src/KnowVault.Application/KnowVaultApplicationModule.cs(1,1)`、`src/KnowVault.HttpApi.Client/KnowVaultHttpApiClientModule.cs(1,1)`，三处均为**历史问题且不在本次改动文件中**（退出码 2）。Task 8 的验收判据据此校正为「无新增违规」

### Task 2/3 —— 多轮历史 + 流式（Ai 层，合并为一次提交）

- **合并原因（计划偏差）**：Task 2 把 `RagPromptBuilder.Build` 的返回值从 `(string System, string User)` 改成 `(string System, IReadOnlyList<LlmChatMessage> Messages)`，而换掉 `ILlmChatService` 是 Task 3 的事——要让这两步之间始终可编译，只能一次性把 Ai 层切成「消息 + 流式」，故 Task 2 与 Task 3 合并提交
- `ILlmChatService.CompleteAsync` **已删除**，只留 `IAsyncEnumerable<string> CompleteStreamAsync(string systemPrompt, IReadOnlyList<LlmChatMessage> messages, CancellationToken cancellationToken = default)`；`DeepSeekChatService` 用 `GetStreamingChatMessageContentsAsync` + `[EnumeratorCancellation]`，丢弃空白首尾帧
- 空回答检查下沉到 `RagService.StreamWithEmptyCheckAsync`（私有异步迭代器），流式与一次性两条路径共享，`AiCallException("DeepSeek 返回了空回答。")` 文案不变
- `IRagService` 现为双入口：`AnswerAsync`（拼流 + `Trim()`）与 `AnswerStreamAsync`（返回 `RagStreamResult { Chunks, Deltas }`）；检索与重排收进私有 `RetrieveAsync`，`RerankAsync` 保持 fail-open
- `RagPromptBuilder` 改为多消息：`Build(question, chunks, history = null)` → `(System, Messages)`；助手历史消息用静态 `Regex(@"\[\d+\]", RegexOptions.Compiled)` 剥掉 `[编号]` 再 `Trim()`，空白内容整条丢弃；**用户历史消息里的方括号不动**；本轮用户消息正文与 P1 逐字一致，引用编号语义不变
- `RagPromptTemplates.SystemPrompt` 增第 5 条：历史只用于理解指代，事实依据必须来自本次资料片段，历史编号已失效
- **Task 2 Step 4 的「历史加载」尚未落地**：`Application/Qa/QaAppService.cs` 仍直接调 `AnswerAsync`，历史读取移到 Task 4 的 `QaStreamService`（REST 与 Hub 共用同一入口，只读一处）
- 测试：`tests/KnowVault.Tests/RagPromptBuilderTests.cs` 全部改签名并新增 4 条（历史顺序 / 剥离引用标记 / 不剥用户方括号 / 丢弃空白助手消息）；`tests/KnowVault.Tests/RagServiceRerankTests.cs` 的假聊天服务改流式，新增 1 条断言两帧拼成 `答案`。`dotnet build KnowVault.slnx` **0 错误 0 警告**，`dotnet test` **32/32 通过**（P1 19 含被改写的 2 条 Prompt 用例 + Task 1 的 8 + 本次新增 5）

### Task 4 —— 流式编排（Application 层）

- 新增 `src/KnowVault.Application.Contracts/Models/Qa/QaStreamEventKindEnum.cs`（Started=0 / Delta=1 / Completed=2）、`src/KnowVault.Application.Contracts/Models/Qa/QaStreamEventDto.cs`、`src/KnowVault.Application.Contracts/Interfaces/IQaStreamService.cs` —— **接口落在 `Interfaces/` 而非计划里写的 `Contracts/Qa/`**，与 P1 的 `Interfaces/IQaAppService.cs` 保持一致
- 新增 `src/KnowVault.Application/Qa/QaStreamService.cs`，继承 `KnowVaultAppService` 并加 `[RemoteService(IsEnabled = false)]`：否则 ABP 会为这个 IApplicationService 生成动态 API 控制器
- **两段显式 UoW**（`_unitOfWorkManager.Begin(requiresNew: true, isTransactional: false)`）：第一段解析会话 + 读历史 + 落用户消息（读完即写，顺序铁律），第二段落助手消息；两段之间是纯模型生成，不持有数据库连接
- 历史加载：先 `OrderByDescending(CreationTime).Take(HistoryMaxMessages)` 再 `OrderBy(CreationTime)`；`HistoryMaxMessages <= 0` 时直接返回空
- `QaAppService.AskAsync` 改为消费同一条事件流并拼接 `Delta`，返回原 `QaAnswerDto`（REST 契约与 P1 一致）；它不再直接依赖仓储与 `IRagService`
- Task 4 不加新单测：Application 层需要 DI + 仓储宿主，按 §0.5 交给 Task 8 端到端验收（Swagger 手测 REST 路径 + 前端降级路径）
- `dotnet build KnowVault.slnx` 0 错误 0 警告，`dotnet test` 32/32 通过


### Task 5 —— SignalR 传输 + 前端流式（2026-10-14）

- 新增 `src/KnowVault.HttpApi/Hubs/QaHub.cs`：`[Authorize] public class QaHub : Hub`、`public const string Route = "/signalr/qa"`、`[HubMethodName("Ask")] IAsyncEnumerable<QaStreamEventDto> AskAsync(AskQuestionInput input)`，取消令牌取自 `Context.ConnectionAborted`（Hub 方法不能声明 CancellationToken，SignalR 会把它当成客户端参数）
- `src/KnowVault.HttpApi/KnowVault.HttpApi.csproj` **一行未改**：`Microsoft.AspNetCore.App` 由 ABP 的 HttpApi 包传递引入，`Hub` 与流式返回直接可用（计划里预判的 `<FrameworkReference>` 兜底没用上）
- 新增 `src/KnowVault.HttpApi.Host/Middleware/SignalRAccessTokenMiddleware.cs`：只对 hub 路径生效（`Path.StartsWithSegments(QaHub.Route)`），无 Authorization 头时把 query string 的 `access_token` 提升为 `Bearer`（OpenIddict 验证不读 query string，WebSocket 又带不了自定义头，这一步是必需的）
- `KnowVaultHttpApiHostModule` 三处装配：`ConfigureServices` 末尾 `context.Services.AddSignalR();`；`app.UseCors();` 之后、`UseAuthentication()` 之前插 `app.UseMiddleware<SignalRAccessTokenMiddleware>();`；末行改 `app.UseConfiguredEndpoints(endpoints => endpoints.MapHub<QaHub>(QaHub.Route));`
- 前端依赖与代理：`package.json` 加 `@microsoft/signalr ^10.0.11`（`npm install` 实装 18 个包）；`vite.config.js` 加 `/signalr` 代理并显式 `ws: true`（不写 ws 时 Vite 只做普通 HTTP 代理，WebSocket 握手必失败）；`src/api/http.js` 抽出并导出 `getToken()`（拦截器改用，行为不变）；新增 `src/api/qaHub.js`（单例连接 + `startQaHub()` + `askStream()` 返回订阅句柄）
- `src/stores/chat.js` 重写：`ask()` 先塞用户消息与助手占位（`streaming: true`）再 `startQaHub()`，连不上走 `askViaRest()` 降级（P1 行为）；`Started` 帧写 `conversationId` 与 `citations`，`Delta` 帧追加正文；`stop()` 用 `subscription.dispose()` 取消并立即收尾——客户端断开后服务端迭代器随即退出，不落半截助手消息；订阅句柄与 resolve 回调都放模块级变量，避免不可序列化对象进 Pinia state
- **踩坑（Vue 响应式）**：`this.messages.push(obj)` 之后再改本地 `obj` 不会触发视图更新（state 里存的是原始对象，读取时才代理），必须 `const assistant = this.messages[this.messages.length - 1]` 取回响应式引用再改
- `src/components/ChatPanel.vue`：助手气泡正文为空时显示「正在生成…」，`streaming` 期间发送按钮换成「停止生成」；`watch` 最后一条消息的 `content` 跟着增量滚到底（流式是追加不是整体替换）；引用标签文案改为「相关性」（重排后 Score 从 cosine 相似度变成 0~1 relevance）
- 验证：`dotnet build KnowVault.slnx` 0 错误 0 警告；`dotnet test` 32/32 通过；`npm install && npm run build` 成功（`dist/assets/index-*.js` 642.29 kB / gzip 199.00 kB，只有 signalr 的 `/*#__PURE__*/` 注解与 chunk 体积两条告警）