# KnowVault P4「演示完整度」实施计划（2026-10-24）

> 执行方式：按 T1 → T9 顺序推进；每个 Task 完成并自测通过后再进入下一个，独立提交，禁止跨任务混改
> 铁律：不改 P1/P2/P3 已验收行为；所有新增异步方法带 `CancellationToken` 并逐层透传；新增 `.cs` 用块级 namespace、`Enum` 后缀、CRLF、UTF-8 无 BOM；新增注释按 `AGENTS.md` 的 XML 规范
> 前置文档：`docs/plans/2026-10-24-knowvault-open-source-roadmap.md`（范围权威）、`TECH_STACK.md`（技术选型权威）
> 分支建议：`feat/p4-product-polish`（自 `main` 切出）
> 本文是 P4 的冻结规格：范围或契约变更必须先改本文的 §0，再改代码

---

## Goal

把 KnowVault 从「功能能跑通」推到「陌生人能独立走完十步」的演示完整产品：

1. 能注册、登录、改密、查看个人信息
2. 会话可保存、切换、重命名、删除，刷新不丢
3. 点引用能看到原文片段，并能打开文档详情查看切片
4. 文档支持批量上传、在线预览（PDF/HTML/Markdown）与切片列表
5. 回答可点赞/点踩、重新生成、复制
6. 知识库可按库配置切片与检索参数
7. 首页有轻量仪表盘（文档数、问答量、耗时、失败数）

对应路线图的陌生人测试第 1–10 步。

## Architecture

```text
前端（Vue 3）
  App.vue ─┬─ OverviewTab（DashboardPanel）
           ├─ QaTab
           │   ├─ qa-side：n-tabs「资料 / 会话」
           │   │   ├─ DocumentPanel（多选上传 + 打开详情）
           │   │   └─ ConversationSidebar（列表 / 重命名 / 删除）
           │   └─ ChatPanel（Markdown 回答 / 引用点击 / 赞踩 / 重生成）
           ├─ KnowledgeBasePanel（+ 设置弹窗）
           └─ ProfileModal / LoginPanel（注册切换）

后端（ABP 分层）
  AccountAppService        注册 / 资料 / 改密        → IdentityUserManager
  ConversationAppService   会话列表 / 消息 / 改名 / 删除 → Conversation + Message
  FeedbackAppService       赞踩                     → MessageFeedback
  DashboardAppService      聚合统计                  → 各仓储
  QaStreamService          消息 ID / 耗时 / 重生成 / 库级参数
  DocumentAppService       切片查询 / 原件读取
  DocumentContentController  GET /api/app/document/{id}/content

数据
  Conversations(+KnowledgeBaseId, +LastMessageAt)
  Messages(+LatencyMs)
  DocumentChunks(+HeadingPath)
  KnowledgeBases(+ChunkSizeChars, +ChunkOverlapChars, +TopK, +SystemPrompt)
  MessageFeedbacks（新表）
```

---

## §0 冻结规格

### 0.1 已确认决策

| 编号 | 决策 | 理由 |
|---|---|---|
| D1 | 注册走自定义 `AccountAppService` + `IdentityUserManager`，注册成功默认加入 `Member` 角色；不启用 ABP Account Web 注册页 | 前端是 Vue，走 REST 可控；应用层已引用 `AbpIdentityApplication` |
| D2 | 会话按 `CreatorId` 隔离；管理员也不查看他人会话；删除会话级联删消息与反馈 | 会话是个人工作区，不是团队资产 |
| D3 | 引用 DTO 直接携带 `Snippet`（正文前 300 字）与 `HeadingPath`；完整原文通过文档切片接口按 `chunkIndex` 获取 | 首屏无需额外请求，点击后再取全文 |
| D4 | 文档内容预览：PDF/HTML/Markdown 在线预览，Word/Excel 仅下载；前端用 axios 取 blob + object URL，iframe 加 `sandbox` | iframe 直链无法携带 Bearer；sandbox 防上传 HTML 的脚本执行 |
| D5 | 批量上传只改前端：`n-upload multiple` + 逐文件 custom-request；后端保持单文件接口 | 避免新增 stream 集合契约，现有去重逻辑直接复用 |
| D6 | 反馈表唯一约束 `(MessageId, UserId)`；`Rating` 传 null 表示取消；只允许反馈自己会话里的助手消息 | 支持点赞/点踩切换，防止越权 |
| D7 | 重新生成：前端移除最后一条助手气泡，带 `Regenerate=true` 重发；后端不重复插入用户消息，历史排除最后一条用户消息 | 不新增 Hub 方法，复用同一编排 |
| D8 | 知识库设置用独立接口 `PUT /api/app/knowledge-base/{id}/settings`；字段可空，null 回落全局配置；只影响新摄入与新问答 | 与名称/描述编辑分离，避免误清空 |
| D9 | 仪表盘只统计当前用户可见知识库与其本人会话；平均耗时取最近 7 天助手消息；趋势按服务器本地日期 | 与权限模型一致，作品规模下内存聚合即可 |
| D10 | 前端引入 `markdown-it` 渲染助手回答，`html: false`；用户消息保持纯文本 | 演示效果必需，关闭 HTML 防 XSS |
| D11 | 流式首帧返回 `UserMessageId`，完成帧返回 `AssistantMessageId`；消息历史接口返回消息 ID 与当前用户反馈 | 反馈、复制、重生成需要稳定消息标识 |
| D12 | 存量数据不回填：新增列一律可空，旧切片 `HeadingPath` 为空、旧消息无耗时 | 遵循仓库「历史文件不动」原则 |

### 0.2 默认项

| 项 | 取值 |
|---|---|
| 会话标题 | 问题前 128 字符（沿用现有逻辑）；重命名上限 256 |
| 引用片段长度 | 前 300 字符，超出加 `…` |
| 切片参数范围 | 长度 200–4000，重叠 0–min(500, 长度/2)，TopK 1–8 |
| 系统提示词 | 最长 4000 字符，空表示使用内置模板 |
| 反馈备注 | 最长 1000 字符，P4 前端暂不提供输入框（预留字段） |
| 消息历史 | 单会话 P4 一次全量返回（作品规模）；P5 再下推分页 |
| 仪表盘趋势 | 最近 7 个自然日 |
| 文档在线预览上限 | 沿用上传大小上限（`Documents:MaxFileSizeMb`） |

### 0.3 数据模型变更

| 表 | 新增列 | 可空 | 说明 |
|---|---|---|---|
| `AppConversations` | `KnowledgeBaseId` (Guid) | 是 | 会话归属知识库，便于按库过滤 |
| `AppConversations` | `LastMessageAt` (DateTime) | 是 | 会话列表排序；为空时回落 `CreationTime` |
| `AppMessages` | `LatencyMs` (int) | 是 | 助手消息生成耗时 |
| `AppDocumentChunks` | `HeadingPath` (string, 512) | 是 | 标题层级路径，供详情展示 |
| `AppKnowledgeBases` | `ChunkSizeChars` (int) | 是 | 为空回落 `Ingest:ChunkSizeChars` |
| `AppKnowledgeBases` | `ChunkOverlapChars` (int) | 是 | 为空回落 `Ingest:ChunkOverlapChars` |
| `AppKnowledgeBases` | `TopK` (int) | 是 | 为空回落 `Ai:TopK` |
| `AppKnowledgeBases` | `SystemPrompt` (string, 4000) | 是 | 为空使用内置模板 |
| `AppMessageFeedbacks` | 新表 | — | `MessageId / UserId / Rating / Comment / TenantId`，唯一索引 `(MessageId, UserId)` |

### 0.4 非目标

- 多租户启用、开放 API、用户管理后台、限流、审计、备份
- URL 抓取、外部数据源、OCR、语义缓存、模型配置界面
- 会话分页、消息搜索、分享链接、回答导出文件
- 文档原文 PDF 内定位到页码（P4 只到切片级）
- 前端 E2E 自动化（P6 可选）

### 0.5 测试策略

- 单测：`DocumentChunker` 新签名、`RagPromptBuilder` 系统提示词覆盖、仪表盘聚合纯函数（如抽出）
- 集成测试（Testcontainers）：会话生命周期、反馈唯一与越权、文档切片查询、知识库设置生效
- 手工验收：§2 十步走查 + 每任务验收项
- 每次提交前：`dotnet build` 0 警告、`dotnet test` 全绿、行尾自查

---

## §1 任务总览与依赖顺序

| Task | 内容 | 依赖 | 迁移 |
|---|---|---|---|
| T1 | 账户体验（注册 / 资料 / 改密） | — | 无 |
| T2 | 会话管理（列表 / 消息 / 改名 / 删除） | — | `P4_ConversationContext` |
| T3 | 引用溯源增强（片段 / 标题路径 / 抽屉） | T4（切片接口） | 无 |
| T4 | 文档详情（切片列表 / 原件预览） | — | `P4_DocumentChunkHeading` |
| T5 | 批量上传（前端多选 / 并发 / 逐条状态） | — | 无 |
| T6 | 问答闭环（消息 ID / 反馈 / 重生成 / 耗时） | T2 | `P4_MessageFeedback` |
| T7 | 知识库设置（切片 / TopK / 系统提示词） | — | `P4_KnowledgeBaseSettings` |
| T8 | 轻量仪表盘 | T6 | 无 |
| T9 | 端到端验收与文档回写 | 全部 | 无 |

建议提交顺序：T1 → T2 → T4 → T3 → T5 → T6 → T7 → T8 → T9。T3 依赖 T4 的切片接口；T8 依赖 T6 的耗时字段。

### 迁移命令

```bash
# 首次使用需安装工具
dotnet tool install --global dotnet-ef

dotnet ef migrations add P4_ConversationContext \
  --project src/KnowVault.EntityFrameworkCore \
  --startup-project src/KnowVault.HttpApi.Host \
  --output-dir Migrations

# 应用迁移（项目约定用 DbMigrator）
dotnet run --project src/KnowVault.DbMigrator
```

---

## Task 1：账户体验

**目标**：新用户可以自己注册，登录后能改显示名和密码。

### Files

- 新增：`src/KnowVault.Application.Contracts/Models/Account/RegisterInput.cs`
- 新增：`src/KnowVault.Application.Contracts/Models/Account/AccountProfileDto.cs`
- 新增：`src/KnowVault.Application.Contracts/Models/Account/UpdateProfileInput.cs`
- 新增：`src/KnowVault.Application.Contracts/Models/Account/ChangePasswordInput.cs`
- 新增：`src/KnowVault.Application.Contracts/Interfaces/IAccountAppService.cs`
- 新增：`src/KnowVault.Application/Account/AccountAppService.cs`
- 修改：`frontend/src/components/LoginPanel.vue`（登录/注册切换）
- 新增：`frontend/src/components/RegisterPanel.vue`
- 新增：`frontend/src/components/ProfileModal.vue`
- 新增：`frontend/src/api/account.js`
- 修改：`frontend/src/App.vue`（header 增加个人中心入口）
- 修改：`frontend/src/stores/auth.js`（登出/用户信息缓存按需）

### 契约

```csharp
/// <summary>
/// 注册入参
/// </summary>
public class RegisterInput
{
    /// <summary>用户名（3–32 位，字母、数字、下划线）</summary>
    [Required]
    [StringLength(32, MinimumLength = 3)]
    public string UserName { get; set; } = string.Empty;

    /// <summary>邮箱</summary>
    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;

    /// <summary>密码（遵循 ABP Identity 默认强度）</summary>
    [Required]
    [StringLength(64, MinimumLength = 6)]
    public string Password { get; set; } = string.Empty;

    /// <summary>显示名，为空时使用用户名</summary>
    [StringLength(64)]
    public string? DisplayName { get; set; }
}
```

```csharp
/// <summary>
/// 当前用户资料
/// </summary>
public class AccountProfileDto
{
    public Guid Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? Email { get; set; }
    public IReadOnlyList<string> Roles { get; set; } = [];
}
```

- `RegisterAsync(RegisterInput, CancellationToken)`：`[AllowAnonymous]`；创建用户 → 加入 `Member` 角色 → 返回 `AccountProfileDto`；Identity 失败统一 `UserFriendlyException`
- `GetProfileAsync(CancellationToken)`：登录用户
- `UpdateProfileAsync(UpdateProfileInput { DisplayName, Email }, CancellationToken)`：更新 `Name` 与 `Email`
- `ChangePasswordAsync(ChangePasswordInput { CurrentPassword, NewPassword }, CancellationToken)`：`IdentityUserManager.ChangePasswordAsync`
- 动态路由：`POST /api/app/account/register`、`GET /api/app/account/profile`、`PUT /api/app/account/profile`、`POST /api/app/account/change-password`

### Steps

1. DTO 与接口落地，注意 `CancellationToken` 放最后
2. `AccountAppService` 注入 `IdentityUserManager`、`IIdentityUserRepository`、`ICurrentUser`、`IClock`
3. 注册前检查用户名/邮箱是否已存在，给出中文提示，不泄露内部错误
4. 注册用户默认加入 `Member`（引用 `KnowVaultIdentityDataSeedContributor.MemberRoleName`）
5. 前端 `LoginPanel` 增加「没有账号？注册」切换；`RegisterPanel` 成功后回到登录并预填用户名
6. `ProfileModal` 展示用户名/邮箱/角色，提供显示名与改密两个表单；改密成功后提示重新登录（P4 不强制踢下线）
7. `App.vue` header 增加用户名按钮，打开 `ProfileModal`

### 验收

- 新用户名注册成功，密码强度不足时提示明确
- 用新账号登录，`Member` 角色生效（可上传文档）
- 重复用户名/邮箱注册被拒绝
- 修改显示名后刷新仍在
- 改密后旧密码失效、新密码可登录

---

## Task 2：会话管理

**目标**：会话不再「刷新即丢」，可列表、切换、重命名、删除。

### Files

- 修改：`src/KnowVault.Domain/Models/Chat/Conversation.cs`
- 新增：`src/KnowVault.Application.Contracts/Models/Chat/ConversationDto.cs`
- 新增：`src/KnowVault.Application.Contracts/Models/Chat/ChatMessageDto.cs`
- 新增：`src/KnowVault.Application.Contracts/Models/Chat/RenameConversationInput.cs`
- 新增：`src/KnowVault.Application.Contracts/Interfaces/IConversationAppService.cs`
- 新增：`src/KnowVault.Application/Chat/ConversationAppService.cs`
- 修改：`src/KnowVault.Application/Qa/QaStreamService.cs`（写 `KnowledgeBaseId`、每轮 `Touch`、会话归属校验）
- 修改：`src/KnowVault.EntityFrameworkCore/EntityFrameworkCore/Configurations/ConversationConfiguration.cs`
- 新增迁移：`P4_ConversationContext`
- 新增：`frontend/src/api/conversations.js`
- 新增：`frontend/src/stores/conversations.js`
- 新增：`frontend/src/components/ConversationSidebar.vue`
- 修改：`frontend/src/App.vue`（`qa-side` 改为「资料 / 会话」两个视图）
- 修改：`frontend/src/stores/chat.js`（加载历史、切换、重命名后同步标题）

### 实体变更

```csharp
/// <summary>会话归属知识库；历史数据为空</summary>
public Guid? KnowledgeBaseId { get; set; }

/// <summary>最后一条消息时间，用于会话列表排序；历史数据为空时回落创建时间</summary>
public DateTime? LastMessageAt { get; set; }

/// <summary>
/// 记录一次会话活动
/// </summary>
/// <param name="at">活动时间</param>
public void Touch(DateTime at) => LastMessageAt = at;
```

- 构造函数增加可选参数 `Guid? knowledgeBaseId = null`
- `ConversationConfiguration`：新增两列，索引改为 `(CreatorId, LastMessageAt)`

### 契约

```csharp
/// <summary>
/// 会话摘要
/// </summary>
public class ConversationDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public Guid? KnowledgeBaseId { get; set; }
    public DateTime CreationTime { get; set; }
    public DateTime? LastMessageAt { get; set; }
}
```

```csharp
/// <summary>
/// 会话中的一条消息
/// </summary>
public class ChatMessageDto
{
    public Guid Id { get; set; }
    public MessageRoleEnum Role { get; set; }
    public string Content { get; set; } = string.Empty;
    public IReadOnlyList<RagCitationDto> Citations { get; set; } = [];
    public DateTime CreationTime { get; set; }
    public int? LatencyMs { get; set; }

    /// <summary>当前用户对这条助手消息的反馈；无反馈为 null</summary>
    public FeedbackRatingEnum? Feedback { get; set; }
}
```

- `GetListAsync(Guid? knowledgeBaseId, CancellationToken)`：按 `CreatorId == CurrentUser.Id` 过滤，`LastMessageAt ?? CreationTime` 倒序
- `GetMessagesAsync(Guid id, CancellationToken)`：校验会话归属，消息按 `CreationTime` 升序，反序列化 `CitationsJson`（解析失败返回空列表，不让旧数据打断页面）
- `RenameAsync(RenameConversationInput input, CancellationToken)`：`EntityDto<Guid>`，校验归属，裁剪空白，长度 1–256
- `DeleteAsync(Guid id, CancellationToken)`：删消息 → 删反馈 → 删会话
- 动态路由：`GET /api/app/conversation?knowledgeBaseId=`、`GET /api/app/conversation/{id}/messages`、`PUT /api/app/conversation`、`DELETE /api/app/conversation/{id}`

### QaStreamService 变更

1. `ResolveConversationAsync` 增加归属校验：`conversation.CreatorId != CurrentUser.Id` 时抛 `AbpAuthorizationException`
2. 新建会话时写入 `KnowledgeBaseId`
3. 每轮完成后 `conversation.Touch(_clock.Now)` 并更新
4. 历史消息加载逻辑不变

### Steps

1. 实体与配置变更 → 生成迁移 → DbMigrator 应用
2. 实现 `ConversationAppService`，注意引用反序列化容错
3. 前端 `conversations` store：`refresh(knowledgeBaseId)`、`load(id)`、`rename(id,title)`、`remove(id)`
4. `chat` store 增加 `loadConversation(id)`：先 `stop()`，再拉消息并替换 `messages`，设置 `conversationId`
5. `ConversationSidebar`：当前会话高亮、双击或菜单重命名、删除二次确认、空态引导
6. `App.vue` 的 `qa-side` 用 `n-tabs` 切换「资料 / 会话」；切换知识库时刷新会话列表
7. 新会话按钮同时清空 `conversationId` 与消息（沿用 `chat.reset()`）

### 验收

- 提一个问题后刷新页面，会话出现在列表里
- 点击历史会话可恢复消息与引用
- 重命名后列表与数据库一致
- 删除会话后消息与反馈一并消失（查库确认）
- 用他人 `conversationId` 提问返回 403/无权，不写入消息

---

## Task 3：引用溯源增强

**目标**：引用不再只是标题，点开能看到命中的原文。

### Files

- 修改：`src/KnowVault.Application.Contracts/Models/Qa/RagCitationDto.cs`
- 修改：`src/KnowVault.Application/Qa/QaStreamService.cs`（映射 `Snippet` / `HeadingPath`）
- 新增：`frontend/src/components/CitationDrawer.vue`
- 修改：`frontend/src/components/ChatPanel.vue`（引用 chip 可点击）
- 修改：`frontend/src/api/documents.js`（复用 T4 的 `getChunks`）

### 契约

```csharp
/// <summary>
/// 引用溯源 DTO
/// </summary>
public class RagCitationDto
{
    public Guid DocumentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
    public double Score { get; set; }

    /// <summary>命中片段正文摘要（前 300 字符）</summary>
    public string? Snippet { get; set; }

    /// <summary>标题层级路径，可为空</summary>
    public string? HeadingPath { get; set; }
}
```

### Steps

1. `QaStreamService` 映射引用时截取 `chunk.Content` 前 300 字符（按字符，不切半个代理对可忽略）
2. 历史消息中的旧引用没有新字段，前端按可选处理
3. `ChatPanel` 的 `cite-chip` 改为 `<button>`，点击设置 `activeCitation`
4. `CitationDrawer` 展示：文档标题、标题路径、片段序号、相似度、片段全文
5. 打开抽屉时调 `documentApi.getChunks(documentId)`，按 `chunkIndex` 取完整正文；加载失败时退回 `snippet`
6. 抽屉底部「查看文档」按钮打开 `DocumentDetailDrawer`（T4）

### 验收

- 回答下方引用可点击，抽屉显示片段全文
- 长片段在抽屉内可滚动，标题路径正确
- 旧会话（无新字段）打开引用不报错，至少显示标题与序号
- 「查看文档」能打开对应文档详情

---

## Task 4：文档详情与在线预览

**目标**：文档不再只有「已就绪」，可以看切片、看原文。

### Files

- 修改：`src/KnowVault.Domain/Models/Documents/DocumentChunk.cs`
- 新增：`src/KnowVault.Application.Contracts/Models/Documents/DocumentChunkDto.cs`
- 修改：`src/KnowVault.Application.Contracts/Interfaces/IDocumentAppService.cs`
- 修改：`src/KnowVault.Application/Documents/DocumentAppService.cs`
- 新增：`src/KnowVault.Application.Contracts/Models/Documents/DocumentContentDto.cs`
- 新增：`src/KnowVault.HttpApi/Controllers/DocumentContentController.cs`
- 修改：`src/KnowVault.Ingest/Processing/DocumentProcessor.cs`（写入 `HeadingPath`）
- 修改：`src/KnowVault.EntityFrameworkCore/EntityFrameworkCore/Configurations/DocumentChunkConfiguration.cs`
- 新增迁移：`P4_DocumentChunkHeading`
- 新增：`frontend/src/components/DocumentDetailDrawer.vue`
- 修改：`frontend/src/components/DocumentPanel.vue`（点击文档打开详情）
- 修改：`frontend/src/api/documents.js`

### 实体与契约

```csharp
/// <summary>标题层级路径，历史数据为空</summary>
public string? HeadingPath { get; set; }
```

```csharp
/// <summary>
/// 文档切片 DTO
/// </summary>
public class DocumentChunkDto
{
    public Guid Id { get; set; }
    public int ChunkIndex { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? HeadingPath { get; set; }
}
```

```csharp
/// <summary>
/// 文档原件内容（仅应用层与控制器之间使用）
/// </summary>
public class DocumentContentDto
{
    public Stream Stream { get; set; } = Stream.Null;
    public string ContentType { get; set; } = "application/octet-stream";
    public string FileName { get; set; } = string.Empty;
}
```

- `GetChunksAsync(Guid id, CancellationToken)`：Viewer 权限，按 `ChunkIndex` 升序
- `GetContentAsync(Guid id, CancellationToken)`：`[RemoteService(IsEnabled = false)]`，Viewer 权限，从对象存储取流；扩展名映射 MIME：

| 扩展名 | Content-Type |
|---|---|
| `.pdf` | `application/pdf` |
| `.docx` | `application/vnd.openxmlformats-officedocument.wordprocessingml.document` |
| `.xlsx` | `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet` |
| `.md` | `text/markdown; charset=utf-8` |
| `.html` | `text/html; charset=utf-8` |
| 其它 | `application/octet-stream` |

- 控制器：`GET /api/app/document/{id}/content?download=false`，返回 `File(stream, contentType, fileName, enableRangeProcessing: true)`；`download=true` 时强制附件下载

### Steps

1. 实体加列 → 迁移 → `DocumentProcessor` 在构造 `DocumentChunk` 时写入 `chunk.HeadingPath`
2. `DocumentAppService.GetChunksAsync` / `GetContentAsync` 落地
3. `DocumentContentController` 显式控制器，路由不与动态 API 冲突
4. 前端 `documentApi.getChunks(id)`、`documentApi.content(id, download)`（`responseType: 'blob'`）
5. `DocumentDetailDrawer`：元数据卡片 + 状态/错误 + 切片列表（序号、标题路径、正文折叠）+ 预览按钮
6. 预览：
   - PDF / HTML：`URL.createObjectURL(blob)` 放入 iframe；HTML 加 `sandbox=""` 禁止脚本，PDF 不加（Chrome 内置阅读器在 sandbox 下白屏）
   - Markdown：读取文本放 `<pre>`，等宽字体
   - Word / Excel：直接触发下载
7. 抽屉关闭时 `URL.revokeObjectURL`，避免内存泄漏
8. `DocumentPanel` 文档标题改为可点击按钮，打开详情

### 验收

- 已就绪文档能列出切片，序号连续、正文与原文一致
- PDF 能在抽屉内预览；HTML 预览不执行脚本；Word/Excel 点击后下载
- 失败文档详情显示错误原因
- 无权用户访问他人文档内容返回 403

---

## Task 5：批量上传

**目标**：一次选多个文件，逐条显示上传与处理状态。

### Files

- 修改：`frontend/src/components/DocumentPanel.vue`
- 修改：`frontend/src/stores/documents.js`
- 修改：`frontend/src/api/documents.js`（无需改契约，仅确认错误提取）

### Steps

1. `n-upload` 增加 `multiple`，保留 `show-file-list` 逐条状态；不设 `:max`——Naive UI 的 max 会把已完成条目累计进去，选够数量后会静默拒绝新选择
2. `documents` store 的 `uploading: boolean` 改为 `uploadingCount: number`；每个文件独立计数
3. 上传成功后不立即全量 refresh，改为 300ms 防抖刷新一次，避免 N 个文件触发 N 次列表请求
4. 失败时把后端 `UserFriendlyException` 文案回传给 `onError`，`n-upload` 对应条目显示红色
5. 同名文件被后端拒绝时，提示需包含文件名，便于用户定位
6. 上传提示补充「可一次选择多个文件；同名文件会被拒绝」

### 验收

- 一次选择 3 个不同文件，列表出现 3 行并各自从待处理走到已就绪
- 其中 1 个与已有文件同名时，仅该条失败，另两条成功
- 批量上传期间按钮 loading，全部结束后恢复
- 列表只刷新一次（看网络面板请求数）

---

## Task 6：问答闭环（反馈 / 重生成 / 耗时 / 消息 ID）

**目标**：回答可以评价、重新生成、复制，反馈能落库。

### Files

- 新增：`src/KnowVault.Domain.Shared/Enum/Chat/FeedbackRatingEnum.cs`
- 新增：`src/KnowVault.Domain/Models/Chat/MessageFeedback.cs`
- 修改：`src/KnowVault.Domain/Models/Chat/Message.cs`
- 修改：`src/KnowVault.Application.Contracts/Models/Qa/AskQuestionInput.cs`
- 修改：`src/KnowVault.Application.Contracts/Models/Qa/QaStreamEventDto.cs`
- 新增：`src/KnowVault.Application.Contracts/Models/Chat/SetFeedbackInput.cs`
- 新增：`src/KnowVault.Application.Contracts/Interfaces/IFeedbackAppService.cs`
- 新增：`src/KnowVault.Application/Chat/FeedbackAppService.cs`
- 修改：`src/KnowVault.Application/Qa/QaStreamService.cs`
- 修改：`src/KnowVault.EntityFrameworkCore/EntityFrameworkCore/Configurations/MessageConfiguration.cs`
- 新增：`src/KnowVault.EntityFrameworkCore/EntityFrameworkCore/Configurations/MessageFeedbackConfiguration.cs`
- 修改：`src/KnowVault.EntityFrameworkCore/EntityFrameworkCore/KnowVaultDbContext.cs`
- 新增迁移：`P4_MessageFeedback`
- 新增：`frontend/src/api/feedback.js`
- 修改：`frontend/src/stores/chat.js`
- 修改：`frontend/src/components/ChatPanel.vue`

### 实体与契约

```csharp
/// <summary>
/// 消息反馈类型
/// </summary>
public enum FeedbackRatingEnum
{
    /// <summary>点赞</summary>
    Like = 0,

    /// <summary>点踩</summary>
    Dislike = 1
}
```

```csharp
/// <summary>
/// 助手消息反馈：同一用户对同一消息只保留一条
/// </summary>
public class MessageFeedback : CreationAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid MessageId { get; set; }
    public Guid UserId { get; set; }
    public FeedbackRatingEnum Rating { get; set; }
    public string? Comment { get; set; }
    public Guid? TenantId { get; set; }
}
```

- `Message` 增加 `public int? LatencyMs { get; private set; }` 与 `SetLatency(int ms)`
- `AskQuestionInput` 增加 `public bool Regenerate { get; set; }`
- `QaStreamEventDto` 增加 `Guid? UserMessageId`、`Guid? AssistantMessageId`、`int? LatencyMs`
- `SetFeedbackInput { Guid MessageId; FeedbackRatingEnum? Rating; string? Comment }`
- `FeedbackAppService.SetAsync(SetFeedbackInput input, CancellationToken)`：
  - 校验消息存在、`Role == Assistant`
  - 校验消息所属会话 `CreatorId == CurrentUser.Id`
  - `Rating == null` 删除已有反馈；否则插入或更新
  - 返回 `FeedbackRatingEnum?`
- 动态路由：`POST /api/app/feedback/set`（ABP 把 `SetAsync` 映射到 `/set` 子路径）

### QaStreamService 变更

1. 方法开始 `Stopwatch.StartNew()`
2. 非重生成：插入用户消息并记下 `userMessage.Id`；重生成：不插入
3. 首帧 `Started` 携带 `UserMessageId`
4. 完成时计算 `LatencyMs`，写入助手消息 `SetLatency`
5. `Completed` 帧携带 `AssistantMessageId`
6. 重生成时 `LoadHistoryAsync` 排除最后一条 `Role == User` 的消息及其后的回答；`input.ConversationId` 为空直接报错
7. 会话 `Touch`（T2 已加）

### 前端变更

- `chat.js`
  - 处理 `EVENT_COMPLETED = 2`，写入 `assistantMessageId`
  - 消息对象增加 `id`、`feedback`、`latencyMs`
  - `feedback(index, rating)`：相同评分再点一次传 null 取消，成功后更新本地状态
  - `regenerate()`：取最后一条用户问题，移除末尾助手消息，带 `regenerate: true` 且不追加用户气泡
  - `copy(index)`：`navigator.clipboard.writeText`
- `ChatPanel.vue`
  - 助手消息底部操作条：赞、踩、复制、重新生成（仅最后一条）
  - 已选评分高亮；请求失败回滚
  - 显示耗时（如 `1.8s`）

### 验收

- 点赞后刷新页面，按钮仍高亮；点踩切换成功；再次点击取消
- 反馈写入 `AppMessageFeedbacks`，同一消息同一用户只有一条
- 重新生成不新增用户消息（查库确认），回答正常替换
- 每条助手消息有耗时，仪表盘可读到
- 复制按钮内容与回答一致

---

## Task 7：知识库设置

**目标**：切片与检索参数按库可调，不再写死全局配置。

### Files

- 修改：`src/KnowVault.Domain/Models/KnowledgeBases/KnowledgeBase.cs`
- 新增：`src/KnowVault.Application.Contracts/Models/KnowledgeBases/KnowledgeBaseSettingsInput.cs`
- 修改：`src/KnowVault.Application.Contracts/Models/KnowledgeBases/KnowledgeBaseDto.cs`
- 修改：`src/KnowVault.Application.Contracts/Interfaces/IKnowledgeBaseAppService.cs`
- 修改：`src/KnowVault.Application/KnowledgeBases/KnowledgeBaseAppService.cs`
- 修改：`src/KnowVault.Ingest/Chunking/IDocumentChunker.cs`
- 修改：`src/KnowVault.Ingest/Chunking/DocumentChunker.cs`
- 新增：`src/KnowVault.Ingest/Chunking/ChunkOptions.cs`
- 修改：`src/KnowVault.Ingest/Processing/DocumentProcessor.cs`
- 修改：`src/KnowVault.EntityFrameworkCore/EntityFrameworkCore/Configurations/KnowledgeBaseConfiguration.cs`
- 新增迁移：`P4_KnowledgeBaseSettings`
- 修改：`src/KnowVault.Ai/Rag/RagModels.cs`（`SystemPromptOverride`）
- 修改：`src/KnowVault.Ai/Rag/RagPromptBuilder.cs`
- 修改：`src/KnowVault.Ai/Rag/RagService.cs`
- 修改：`src/KnowVault.Application/Qa/QaStreamService.cs`（读库设置）
- 修改：`tests/KnowVault.Tests/DocumentChunkerTests.cs`
- 修改：`tests/KnowVault.Tests/RagPromptBuilderTests.cs`
- 修改：`frontend/src/api/knowledgeBases.js`
- 修改：`frontend/src/components/KnowledgeBasePanel.vue`
- 修改：`frontend/src/stores/documents.js`

### 契约

```csharp
/// <summary>
/// 知识库设置入参
/// </summary>
public class KnowledgeBaseSettingsInput : EntityDto<Guid>
{
    /// <summary>切片长度（字符），为空回落全局配置</summary>
    public int? ChunkSizeChars { get; set; }

    /// <summary>切片重叠（字符），为空回落全局配置</summary>
    public int? ChunkOverlapChars { get; set; }

    /// <summary>检索条数，为空回落全局配置</summary>
    public int? TopK { get; set; }

    /// <summary>系统提示词，为空使用内置模板</summary>
    [StringLength(4000)]
    public string? SystemPrompt { get; set; }
}
```

- `UpdateSettingsAsync(KnowledgeBaseSettingsInput input, CancellationToken)`：Owner 权限
- 校验：`ChunkSizeChars` 200–4000；`ChunkOverlapChars` 0–min(500, size/2)；`TopK` 1–8
- `KnowledgeBaseDto` 增加四个设置字段，列表接口回填
- 动态路由：`PUT /api/app/knowledge-base/{id}/settings`

### 切片参数

```csharp
/// <summary>
/// 一次切片使用的参数
/// </summary>
public class ChunkOptions
{
    /// <summary>单片目标长度（字符）</summary>
    public int SizeChars { get; set; } = 1000;

    /// <summary>相邻切片重叠（字符）</summary>
    public int OverlapChars { get; set; } = 120;
}
```

- `IDocumentChunker` 改为 `Chunk(ParsedDocument document, ChunkOptions options)`
- `DocumentChunker` 不再注入 `IOptions<IngestOptions>`，由调用方决定参数
- `DocumentProcessor` 注入 `IRepository<KnowledgeBase, Guid>` 与 `IOptions<IngestOptions>`：
  - 查文档所属知识库
  - `SizeChars = kb?.ChunkSizeChars ?? options.ChunkSizeChars`
  - `OverlapChars = kb?.ChunkOverlapChars ?? options.ChunkOverlapChars`
- 更新 `DocumentChunkerTests` 使用显式 `ChunkOptions`

### 检索参数

- `RagRequest` 增加 `public string? SystemPromptOverride { get; set; }`
- `RagPromptBuilder.Build` 增加可选参数 `string? systemPromptOverride = null`，非空时替换内置模板
- `RagService` 调用 `Build(question, chunks, history, request.SystemPromptOverride)`
- `QaStreamService` 查知识库：
  - `TopK` 优先取请求参数，其次库设置，最后 0（交给全局配置）
  - `SystemPromptOverride = kb?.SystemPrompt`

### Steps

1. 实体与配置 → 迁移 → 应用
2. 设置接口与校验
3. 切片参数贯通 Ingest，更新单测
4. 提示词覆盖贯通 Ai，更新单测
5. 前端 `KnowledgeBasePanel` 卡片增加「设置」按钮与弹窗，表单带占位提示（如「留空使用全局 1000」）
6. 保存后刷新列表；提示「仅对新上传的文档与新提问生效」

### 验收

- 将某库切片长度改为 500 后上传长文档，切片数明显多于默认库
- 重叠参数超过长度一半时被后端拒绝并给出中文提示
- 设置 TopK=1 后该库回答引用数不超过 1
- 设置系统提示词后，回答风格变化（如只输出要点）
- 其它库不受影响

---

## Task 8：轻量仪表盘

**目标**：首页能看到产品运行状态，演示时一眼有数据。

### Files

- 新增：`src/KnowVault.Application.Contracts/Models/Dashboard/DashboardDto.cs`
- 新增：`src/KnowVault.Application.Contracts/Models/Dashboard/DashboardTrendPointDto.cs`
- 新增：`src/KnowVault.Application.Contracts/Interfaces/IDashboardAppService.cs`
- 新增：`src/KnowVault.Application/Dashboard/DashboardAppService.cs`
- 新增：`frontend/src/api/dashboard.js`
- 新增：`frontend/src/components/DashboardPanel.vue`
- 修改：`frontend/src/App.vue`（新增「概览」tab）
- 修改：`frontend/package.json`（无新增依赖，ECharts 已存在）

### 契约

```csharp
/// <summary>
/// 仪表盘汇总
/// </summary>
public class DashboardDto
{
    public int KnowledgeBaseCount { get; set; }
    public int DocumentCount { get; set; }
    public int ReadyDocumentCount { get; set; }
    public int FailedDocumentCount { get; set; }
    public int ConversationCount { get; set; }
    public int QuestionCount { get; set; }
    public int TodayQuestionCount { get; set; }
    public double AverageLatencyMs { get; set; }
    public IReadOnlyList<DashboardTrendPointDto> Last7DayQuestions { get; set; } = [];
}
```

```csharp
/// <summary>
/// 仪表盘趋势点
/// </summary>
public class DashboardTrendPointDto
{
    /// <summary>日期（服务器本地时区）</summary>
    public DateTime Date { get; set; }

    /// <summary>当天提问数</summary>
    public int Count { get; set; }
}
```

### 统计口径

| 指标 | 口径 |
|---|---|
| 知识库数 | 当前用户可见知识库（`GetAccessibleIdsAsync`） |
| 文档数 / 就绪 / 失败 | 上述知识库下的文档 |
| 会话数 | 当前用户创建 |
| 问题数 | 当前用户会话中的用户消息数 |
| 今日问题数 | 上述消息中 `CreationTime` 落在服务器本地今天 |
| 平均耗时 | 当前用户会话中 `LatencyMs != null` 的助手消息，最近 7 天均值 |
| 7 天趋势 | 当前用户用户消息按本地日期分组 |

- 作品规模下用仓储 `GetListAsync` 后内存聚合；P5 再下推 SQL
- 动态路由：`GET /api/app/dashboard`
- 前端：4 个数字卡（文档、就绪、问答、平均耗时）+ 一张 ECharts 折线（7 天提问趋势）+ 失败文档提示条

### Steps

1. 契约与服务落地，注意空数据返回 0 而不是异常
2. 前端 `DashboardPanel` 引入 ECharts，注意组件卸载时 `dispose`
3. `App.vue` 增加「概览」tab，放在「问答」之前，作为默认页
4. 数字卡片支持加载骨架；失败时显示重试
5. 失败文档数大于 0 时显示可跳转「问答」页的提示

### 验收

- 新建库、传文档、问几个问题后，仪表盘数字与页面一致
- 故意让一个文档失败（如损坏文件），失败数 +1
- 7 天趋势有当天数据；无数据时显示空态而不是报错
- 切换账号后只看到自己的会话统计

---

## Task 9：端到端验收与文档回写

### Steps

1. 按 §2 十步逐项走查，截图留证
2. 跑 `dotnet build`、`dotnet test`
3. 行尾自查：本次改动的每个文件 CRLF 与 loneLF 必须有一个为 0（`AGENTS.md`/`.editorconfig` 例外）
4. 回写文档：
   - `TECH_STACK.md` §6 标记 P4 进度，§9 增加 P4 定稿
   - `AGENTS.md` §七 更新 P4 完成状态
   - 新增 `docs/P4-代码导读.md`（按主题索引 + 阅读顺序）
5. 在本文末尾补执行记录（实测偏差、踩坑、验收证据）

### 验收

- §2 全部通过
- 构建 0 警告、测试全绿
- 文档回写完成

---

## §2 端到端验收清单

在开发机上启动 Aspire 与前端后逐项打勾，并截图：

- [ ] 1. 一条命令启动后端、前端与依赖（Aspire）
- [ ] 2. 打开注册页，注册新账号并登录
- [ ] 3. 新建知识库
- [ ] 4. 一次选择 3 个不同格式文档上传，全部走到已就绪
- [ ] 5. 打开文档详情，看到切片列表；PDF 可在线预览
- [ ] 6. 提问，回答以 Markdown 渲染并逐字流出
- [ ] 7. 点击引用，看到原文片段；点「查看文档」打开详情
- [ ] 8. 刷新页面，从会话列表恢复历史并继续追问
- [ ] 9. 点赞、点踩、重新生成、复制各一次
- [ ] 10. 概览页数字与实际一致，7 天趋势有数据
- [ ] 11. 修改知识库切片参数后新文档切片数变化
- [ ] 12. 个人中心改显示名、改密各一次
- [ ] 13. 用 Viewer 账号访问无权限知识库被拒绝
- [ ] 14. 刷新后反馈状态仍高亮
- [ ] 15. 构建 0 警告、单测与集成测试全绿

---

## §3 风险登记

| 风险 | 影响 | 应对 |
|---|---|---|
| blob 预览大文件占用内存 | 大 PDF 卡顿 | 沿用上传上限；抽屉关闭立即 revoke；后续可改 Range 直链 |
| 上传 HTML 预览执行脚本 | XSS | iframe `sandbox` 留空，禁止脚本 |
| Markdown 渲染 XSS | 回答注入 HTML | `markdown-it` 配置 `html: false` |
| 重生成逻辑漏排除最后一条用户消息 | 重复上下文、回答漂移 | 集成测试断言重生成后用户消息数不变 |
| 会话归属校验遗漏 | 越权续写他人会话 | 所有会话读写入口统一走归属校验，集成测试覆盖 |
| 批量上传触发大量刷新 | 页面抖动、请求风暴 | 300ms 防抖 + 上传计数 |
| 库设置改动影响存量 | 切片不一致 | 明确只影响新文档；设置弹窗提示 |
| 新增列可空导致前端空值 | 页面报错 | 前端全部按可选处理，历史数据不迁移 |
| 迁移遗漏 | 应用启动失败 | 每个 Task 独立迁移并在 DbMigrator 后自测 |

---

## §4 完成定义（DoD）

- §2 十五项验收全部通过并留证
- `dotnet build` 0 警告，`dotnet test` 全绿
- 新增代码符合 namespace / 枚举后缀 / CancellationToken / 注释 / 行尾规范
- 每个 Task 有独立提交，提交信息使用 Conventional Commits
- `TECH_STACK.md`、`AGENTS.md`、`docs/P4-代码导读.md` 已回写
- 演示路径无阻塞：注册 → 建库 → 批量导入 → 预览 → 问答 → 引用 → 历史 → 反馈 → 仪表盘

---

## §5 执行记录

> 边做边记：实测偏差、根因、修复方式、验收证据。格式参照 P3 文档 §5。

| 日期 | Task | 记录 | 证据 |
|---|---|---|---|
| — | — | （待执行） | — |
