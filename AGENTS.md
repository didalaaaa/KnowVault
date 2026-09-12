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
| 异步 | RabbitMQ / MassTransit |
| AI 模型 | **DeepSeek**（生成） + **Qwen**（Embedding + Rerank，跨厂商路由） |
| 前端 | Vue 3 + Vite + **JavaScript**（不用 TS）+ Naive UI + Pinia |
| 本地编排 | .NET Aspire｜Docker Compose |
| 可观测 | OpenTelemetry + Seq + Serilog |
| 测试 | xUnit + Testcontainers + FluentAssertions |

---

## 二、项目结构

```
KnowVault.slnx
├─ src/
│  ├─ KnowVault.Domain.Shared         // 共享枚举 / 常量 / 本地化资源（跨层复用类型）
│  ├─ KnowVault.Domain                // 实体 / 聚合 / 仓储接口（多租户预留）
│  ├─ KnowVault.Application.Contracts // AppService 契约 + DTO
│  ├─ KnowVault.Application           // AppService 用例编排 + AI 编排入口
│  ├─ KnowVault.EntityFrameworkCore   // EF Core(PostgreSQL) + 迁移；Qdrant 访问封装
│  ├─ KnowVault.Ai                   // Semantic Kernel：Prompt / RAG / 向量检索 / 重排 / 流式
│  ├─ KnowVault.Ingest               // 文档解析后台 Worker（消费 RabbitMQ，产出切片+向量）
│  ├─ KnowVault.HttpApi              // 动态 API 控制器（ABP 自动生成）
│  ├─ KnowVault.HttpApi.Client       // HttpClient 强类型代理（供外部调用方使用）
│  ├─ KnowVault.HttpApi.Host         // 主宿主（OpenIddict/健康检查/Swagger），Aspire 编排入口
│  └─ KnowVault.DbMigrator           // 数据库迁移 / 种子执行器（EF 迁移入口）
├─ aspire/
│  └─ KnowVault.AppHost              // Aspire 编排：Postgres/Redis/Qdrant/MinIO/RabbitMQ/Seq + API
├─ frontend/                          // Vue3 + Vite + JS + Naive UI
└─ tests/                             // （待建）Testcontainers 集成测试
```

> **当前实现（P0 ABP 分层模板）**：主宿主为 ABP 约定的 `KnowVault.HttpApi.Host`（非 `KnowVault.Web`），并含 `Domain.Shared` / `Application.Contracts` / `HttpApi.Client` / `DbMigrator` 等标准层；`tests/` 尚未创建。技术选型 / 架构 / 数据模型 / 路线图的权威定义见 `TECH_STACK.md`。

---

## 三、语言与风格约定

### 后端（C# / .NET / ABP）
- 语言版本随 .NET 10（C# 14），`Nullable` 开启。
- 遵循 **ABP 约定**：应用服务命名以 `AppService` 结尾；实体继承 `Entity` / `AggregateRoot`；公共方法遵循 .NET 异步命名（`XXXAsync`）。
- **分层职责清晰**：Domain 不放 EF/DTO；Application 做用例编排，不含 SQL；EF Core 只做持久化；AI 逻辑只放 `KnowVault.Ai`。
- 所有业务实体**预留多租户**：表带 `TenantId`（ABP 一等公民，先单租户运行不用强制隔离，但结构保留）。
- 主键用 GUID（ABP 默认 `Guid`），需要仓储接口定义于 Domain、实现于 EF Core。
- **namespace 规则（强制执行）**：namespace 首段必须等于**项目名**，子目录再在项目名后逐级追加。例如 `src/KnowVault.HttpApi.Host/Program.cs` → `KnowVault.HttpApi.Host`；`src/KnowVault.HttpApi.Host/HealthChecks/X.cs` → `KnowVault.HttpApi.Host.HealthChecks`；`src/KnowVault.Domain/Documents/Document.cs` → `KnowVault.Domain.Documents`。**禁止**用统一根命名空间 `KnowVault` 平铺（如 `namespace KnowVault;`）。
- **namespace 一律使用块级（block-scoped）声明**：`.editorconfig` 已强制 `csharp_style_namespace_declarations = block_scoped:error`；禁用 file-scoped（`namespace X;`），否则触发 IDE0160。
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
  - 类/接口：`/// <summary>…</summary>`（一句话说明职责）。
  - 方法：`/// <summary>…</summary>` + `/// <param name="x">…</param>` + `/// <returns>…</returns>`。
  - 属性/字段/枚举值：`/// <summary>…</summary>`（说明含义、取值范围或约束）。
  - 构造函数：`/// <summary>` + 逐个 `/// <param>`。
  - 常用标签：`<see cref="..."/>`、`<remarks>`、`<c>code</c>`、`<exception cref="...">`。
  - 复杂算法/易误解逻辑：行内 `//` 说明，放在语句上方。
- **C# 自动生成文件**（EF 迁移 `Migrations/*`、`*.Designer.cs`、`AssemblyInfo.cs`）不强制注释，勿随意改动。
- **前端（JS / Vue）**：
  - 具名函数/工具用 JSDoc：`/** ... @param {...} ... @returns {...} */`。
  - 组件（`.vue`）在 `<script setup>` 内：`ref` 状态、具名函数、`onMounted` 逻辑用 `//` 或 JSDoc 说明；模板可用 `<!-- ... -->` 说明复杂块。
  - Pinia store：state 字段与 action 用 JSDoc/`//` 说明。
- **注释与代码同源**：改动代码时同步更新其注释；新增文件按此规范书写。

### 行尾与换行规范（强制执行）
- **统一使用 LF（`\n`）**：仓库根 `.editorconfig`（`root = true`）对 `[*]` 已声明 `end_of_line = lf`。所有文本文件（`.cs` / `.cshtml` / `.json` / `.js` / `.vue` / `.md` / `.props` / `.slnx` 等）一律 LF。
- **禁止单个文件混用行尾**：同一个文件若同时含 `CRLF`（`\r\n`）与 `LF`（`\n`），VS2026 会弹「不一致的行尾，是否将行尾标准化？」提示，且与 `.editorconfig` 的 `lf` 约定冲突。
- **已知根因（曾发生）**：P0 用 `abp new`（ABP Studio CLI 在 Windows 生成，倾向 CRLF）搭建后，又被多种工具轮番改写——某个 PowerShell 脚本用 `[IO.File]::WriteAllText(...-join "\n"...)` 整文件重写成 LF，后续补丁式编辑工具又把少数结构性行（namespace 开括号、个别方法收尾 `}` 与空行）局部回写为 CRLF，最终一个文件两种换行，触发该提示。
- **预防（生成/改动代码后必做）**：
  1. 用脚手架/工具生成产物（`abp new`、`dotnet new`、`npm` 等）后，**立刻将产物统一规范到 LF**。
  2. 人工或工具编辑文件时，保持与文件现有换行一致（即 LF）；**不要在同一文件里混用 CRLF 与 LF**。
  3. 提交/交付前检测：任一文本文件同时含 CRLF 与 LF 即视为不合规，须归一化；若某文件仅整体 CRLF（未混合），也应统一为 LF 以匹配 `.editorconfig`。
  4. 归一化手段（无需 git 即可用）：
     ```powershell
     $p='<文件路径>'; [IO.File]::WriteAllText($p, ([IO.File]::ReadAllText($p) -replace "`r`n","`n") -replace "`r","`n")
     ```
     或对整目录批量处理；本仓库未初始化 git，不用依赖 `.gitattributes`。
- **例外**：无。全部文本文件统一 LF。

---

## 四、数据与 AI 层约定（红线）

- **Qdrant**：**单一 collection + payload 过滤**。每个向量的 payload 必须带 `tenantId / knowledgeBaseId / documentId / chunkIndex`；禁止为每个知识库各建一个 collection。
- **切片 Chunking**：按段落/标题层级 + 固定长度；默认 chunk≈500–800 token、overlap≈50–100；参数可配置（走设置），并保留段落/标题 metadata 供引用溯源。
- **检索 + 组装**：按 `tenantId + knowledgeBaseId` 做 payload 过滤，top-k≈5–8；System prompt 规定「只根据资料回答、资料不足就说不知道、标注来源」；把 top-k 切片 + 问题交给 DeepSeek，返回 `answer + citations`。
- **模型路由**：生成=DeepSeek，向量化/重排=Qwen。统一封装成 OpenAI 兼容调用层，便于日后换厂商。
- **文档解析库**：PDF=`PdfPig`、Word=`DocumentFormat.OpenXml`、Excel=`NPOI`/`ClosedXML`、Markdown=`Markdig`、HTML=`HtmlAgilityPack`。
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
- **API Key（DeepSeek/Qwen）放环境变量**，**严禁进仓库/hardcode**（用 `.env` + 环境变量注入；变量清单参照根目录 `.env.example`）。
- 提交/PR 前保证 `dotnet build` 无错误、`dotnet test` 通过。

---

## 六、DSH / Agent 协作约定

- 改动代码前先 `read` 相关文件，遵循既有结构与命名习惯；新增文件遵循第四节约定。
- 遇到「AI 编排 / RAG / 向量检索」类问题，优先看 `src/KnowVault.Ai`，不要散落到其它分层。
- 涉及多租户隔离、Qdrant 过滤、切片策略等**关键设计**改动时，同步更新 `TECH_STACK.md` 与本文档，保持两者一致。
- 这是一个 **AI + 架构双主线**的提升竞争力的个人项目：优先保证**可演示的 RAG 闭环**，其次才是纵深优化。

---

## 七、待办/注意

- DeepSeek / Qwen 的 API Key 尚未配置（搭骨架时可先用 Mock / 占位注入，不阻塞）。
- `src/`、`frontend/` 已落地为 ABP 分层骨架（P0）；`tests/`（Testcontainers 集成测试）尚未创建，按 `TECH_STACK.md` §3 / §5 的范围补齐。
