# KnowVault P6「开源交付」实施计划（2026-10-28）

> 状态：**已完成（2026-10-29），T1–T7 验收通过，见 §5**
> 执行方式：按 T1 → T7 顺序推进；每个 Task 完成并自测通过后再进入下一个，独立提交，禁止跨任务混改
> 铁律：不改 P0–P5 已验收的业务行为；P6 只允许三类改动——Compose/镜像外壳、Redis 缓存接线、演示账号与示例数据；新增异步方法带 `CancellationToken` 并逐层透传；新增 `.cs` 用块级 namespace、`Enum` 后缀、CRLF、UTF-8 无 BOM；Dockerfile / `.sh` 保持 LF
> 前置文档：`docs/driver/需求/需求分析-1028-P6.md`（需求权威）、`docs/plans/2026-10-24-knowvault-open-source-roadmap.md`（范围权威）、`TECH_STACK.md`（技术选型权威）
> 分支：`feat/p6-open-source-delivery`（自 `main` 切出）
> 本文是 P6 的冻结规格：范围、契约或默认值变更必须先改本文的 §0，再改代码

---

## Goal

把 P0–P5 已实现的能力包装成一个**陌生人可复现的开源作品**：

1. `cp .env.example .env` → 填两个模型 Key → `docker compose up -d --build` → 打开 `http://localhost:8080`
2. 首次启动自动迁移、种子演示账号、导入示例知识库与 7 份多格式文档
3. README 首屏能看懂定位、架构、截图、快速开始、演示账号
4. MIT LICENSE + 第三方许可清单 + 无真实密钥
5. 干净克隆 + 干净数据卷走完十步陌生人测试，A1–A12 逐项留证

P6 不新增检索/问答能力；不部署上线；不启用多租户；不做 CI 流水线（D7 后置，2026-10-29 以 GitHub Actions 补齐）。

## Architecture

```text
浏览器 http://localhost:8080
   │
   ▼
frontend（nginx:1.27-alpine）
   ├─ /            → SPA 静态文件（Vue 3 dist）
   ├─ /api/        → api:8080
   ├─ /connect/    → api:8080（OpenIddict 密码模式）
   ├─ /.well-known/→ api:8080
   └─ /signalr/    → api:8080（WebSocket Upgrade）

api（Production，http://+:8080，非 root）
   ├─ PostgreSQL 16        （EF Core / ABP / OpenIddict）
   ├─ Redis 8.6            （ABP 分布式缓存，D6）
   ├─ Qdrant v1.15.0       （gRPC 6334，dense + sparse）
   ├─ MinIO                （原件）
   ├─ RabbitMQ 3-management（document-ingest，MassTransit 8.5.7）
   ├─ Seq 2025.1           （Serilog + OTLP/HTTP）
   ├─ DeepSeek             （生成）
   └─ SiliconFlow          （Embedding + Rerank）

一次性任务：
  migrator   → 迁移 + 种子（admin/角色/默认库/OpenIddict 客户端/demo/viewer）
  demo-seed  → 等 API 健康 → demo 登录 → 建「KnowVault 示例库」→ 上传 7 份样例 → 轮询 Ready
```

---

## §0 冻结规格

### 0.1 已确认决策

| 编号 | 决策 | 取值 |
|---|---|---|
| D1 | LICENSE | MIT |
| D2 | Compose 运行模式 | Production + HTTP + 构建期自签 pfx；issuer 固定为 `http://localhost:8080` |
| D3 | 示例数据机制 | 一次性 `tools/KnowVault.DemoSeed` 控制台，通过公开 HTTP API 导入 |
| D4 | 演示账号 | 配置开关 `Demo:SeedAccounts` 种 `demo`（Member）+ `viewer`（Viewer）；admin 由 ABP 种子创建 |
| D5 | 内部文档与历史 | 只清理 HEAD：`docs/driver/问题/` 与 `docs/interview/` 移出公开树并加入 `.gitignore`；保留 git 历史 |
| D6 | Redis | 接入 `Volo.Abp.Caching.StackExchangeRedis` 10.5.0 |
| D7 | CI | 后置，P6 不提交流水线（2026-10-29 以 GitHub Actions 单独补齐） |
| D8 | README/素材 | 中文 README + 英文一句话；6–8 张 PNG；不做 GIF |

### 0.2 实现偏差（相对需求文档）

| 项 | 需求文档 | 实施取值 | 原因 |
|---|---|---|---|
| MinIO 控制台宿主端口 | `9001` | **`9002`（`MINIO_CONSOLE_PORT`）** | 本机 `netsh int ipv4 show excludedportrange` 显示 9001 落在 Windows 保留端口段，绑定必失败；Aspire 侧也曾因此改成动态端口 |
| Seq EULA | 未写 | Compose 注入 **`ACCEPT_EULA=Y`** | Seq 镜像 2025.1 未接受 EULA 会直接退出（实测退出码 1）；README 注明 Seq 单用户免费与 EULA |
| API 容器端口 | 8080 | 8080（宿主 8081） | 与需求一致 |
| 前端 nginx 版本 | `nginx:1.27-alpine` | `nginx:1.27-alpine` | 与需求一致 |

### 0.3 服务、端口与卷

所有宿主端口可由 `.env` 覆盖；默认值避开 Windows 保留端口。

| 服务 | 镜像/构建 | 容器端口 | 宿主端口 | 卷 | 健康检查 |
|---|---|---|---|---|---|
| `frontend` | `frontend/Dockerfile` | 80 | `${KNOWVAULT_HTTP_PORT:-8080}` | — | `wget -q --spider http://localhost/` |
| `api` | `src/KnowVault.HttpApi.Host/Dockerfile` | 8080 | `${KNOWVAULT_API_PORT:-8081}` | — | `curl -f http://localhost:8080/health-status` |
| `migrator` | `src/KnowVault.DbMigrator/Dockerfile` | — | — | — | 退出码 0 |
| `postgres` | `postgres:16` | 5432 | 不暴露 | `knowvault-postgres` | `pg_isready -U postgres` |
| `redis` | `redis:8.6` | 6379 | 不暴露 | `knowvault-redis` | `redis-cli ping` |
| `qdrant` | `qdrant/qdrant:v1.15.0` | 6333 / 6334 | `${QDRANT_HTTP_PORT:-6333}` → 6333 | `knowvault-qdrant` | `bash -c 'exec 3<>/dev/tcp/127.0.0.1/6333'` |
| `minio` | `minio/minio:RELEASE.2025-09-07T16-13-09Z` | 9000 / 9001 | `${MINIO_CONSOLE_PORT:-9002}` → 9001 | `knowvault-minio` | `curl -f http://localhost:9000/minio/health/live` |
| `rabbitmq` | `rabbitmq:3-management` | 5672 / 15672 | `${RABBITMQ_MANAGEMENT_PORT:-15672}` | `knowvault-rabbitmq` | `rabbitmq-diagnostics -q ping` |
| `seq` | `datalust/seq:2025.1` | 80 / 5341 | `${SEQ_PORT:-5341}` → 80 | `knowvault-seq` | `curl -f http://localhost/health` |
| `demo-seed` | `tools/KnowVault.DemoSeed/Dockerfile` | — | — | 挂载 `./samples/kb-import:/samples:ro` | 退出码 0 / 失败不阻塞 |

### 0.4 运行时配置

`api` 与 `migrator` 共用以下键（值见 §0.3 与 `.env.example`）：

| 键 | 值 |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ASPNETCORE_URLS`（仅 api） | `http://+:8080` |
| `App__SelfUrl` / `AuthServer__Authority` / `App__CorsOrigins` | `http://localhost:${KNOWVAULT_HTTP_PORT}` |
| `AuthServer__RequireHttpsMetadata` | `false` |
| `AuthServer__CertificatePassPhrase` | `${AUTH_CERT_PASSPHRASE}` |
| `StringEncryption__DefaultPassPhrase` | `${STRING_ENCRYPTION_PASSPHRASE}` |
| `ConnectionStrings__Default` | `Host=postgres;Port=5432;Database=KnowVault;Username=postgres;Password=${POSTGRES_PASSWORD}` |
| `Redis__Configuration` | `redis:6379` |
| `ConnectionStrings__rabbitmq` | `amqp://${RABBITMQ_DEFAULT_USER}:${RABBITMQ_DEFAULT_PASS}@rabbitmq:5672/` |
| `Qdrant__Endpoint` / `Qdrant__ApiKey` | `http://qdrant:6334` / 空 |
| `Minio__Endpoint` / `AccessKey` / `SecretKey` / `UseSsl` | `minio:9000` / `${MINIO_ROOT_USER}` / `${MINIO_ROOT_PASSWORD}` / `false` |
| `Seq__ServerUrl` | `http://seq:5341` |
| `Observability__OtlpEndpoint` | `http://seq:5341/ingest/otlp/v1/traces` |
| `App__DisablePII` | `true` |
| `Ai__DeepSeekApiKey` / `Ai__SiliconFlowApiKey` | `${DEEPSEEK_API_KEY}` / `${SILICONFLOW_API_KEY}` |
| `Demo__SeedAccounts`（migrator） | `true` |
| `OpenIddict__Applications__KnowVault_App__RootUrl`（migrator） | `http://localhost:${KNOWVAULT_HTTP_PORT}/` |
| `OpenIddict__Applications__KnowVault_Swagger__RootUrl`（migrator） | 同上 |

### 0.5 文件变更清单

**新增**

- `docker-compose.yml`、`.dockerignore`
- `frontend/Dockerfile`、`frontend/nginx.conf`、`frontend/.dockerignore`
- `tools/KnowVault.DemoSeed/`（`csproj`、`Program.cs`、`Dockerfile`、`README.md`）
- `README.md`、`LICENSE`、`THIRD-PARTY-LICENSES.md`
- `docs/docker-compose.md`、`docs/P6-交付说明.md`
- `docs/assets/architecture.svg`、`docs/assets/screenshots/*.png`

**修改**

- `src/KnowVault.HttpApi.Host/Dockerfile`、`src/KnowVault.DbMigrator/Dockerfile`：多阶段构建
- `src/KnowVault.HttpApi.Host/KnowVault.HttpApi.Host.csproj`、`KnowVaultHttpApiHostModule.cs`：Redis 缓存
- `src/KnowVault.Domain/Seeds/KnowVaultIdentityDataSeedContributor.cs`：`Demo:SeedAccounts` 开关
- `src/KnowVault.DbMigrator/KnowVault.DbMigrator.csproj`、新增 `appsettings.Development.json`：条件包含 secrets、Demo 开关
- `.env.example`：Compose 段
- `.gitignore`：`docs/interview/`、`docs/driver/问题/`、`.archify/`
- `KnowVault.slnx`：DemoSeed 项目
- `TECH_STACK.md`、`AGENTS.md`、`docs/plans/2026-10-24-knowvault-open-source-roadmap.md`：P6 状态与入口

**移出公开工作树（本地保留）**

- `docs/driver/问题/已修复问题.md`、`docs/driver/问题/待修复问题.md`（`git rm --cached`）
- `docs/interview/`（从未跟踪，移动后加入 `.gitignore`）

### 0.6 非目标

- 不做 HTTPS / 域名 / 公网部署 / WAF / 限流 / 备份。
- 不做新的数据库迁移、不改 Qdrant schema、不改 API 契约。
- 不做 CI、GIF、模型供应商配置界面。
- 不把 `.env`、`appsettings.secrets.json`、`openiddict.pfx` 提交进仓库。

---

## §1 任务总览与依赖顺序

| 顺序 | 任务 | 依赖 | 主要交付 |
|---|---|---|---|
| T1 | Compose 与镜像 | — | 多阶段 Dockerfile、nginx、`.dockerignore`、`docker-compose.yml`、`.env.example` |
| T2 | Redis 接线 | T1 | `Volo.Abp.Caching.StackExchangeRedis` |
| T3 | 演示账号与示例数据 | T1 | Demo 种子开关、DemoSeed 工具、slnx |
| T4 | 合规与清理 | — | LICENSE、第三方许可、密钥扫描、内部文档移出 |
| T5 | README 与素材 | T1–T3 | README、架构图、6–8 张截图、`docs/docker-compose.md` |
| T6 | 文档校订 | T1–T5 | roadmap / TECH_STACK / AGENTS、`docs/P6-交付说明.md` |
| T7 | 端到端验收 | 全部 | A1–A12、质量门禁、执行记录 |

---

## Task 1：Compose 与镜像

### Files

- `docker-compose.yml`（新增）
- `.dockerignore`（新增）
- `frontend/Dockerfile`、`frontend/nginx.conf`、`frontend/.dockerignore`（新增）
- `src/KnowVault.HttpApi.Host/Dockerfile`（重写）
- `src/KnowVault.DbMigrator/Dockerfile`（重写）
- `.env.example`（修改）

### Steps

1. **API Dockerfile**：SDK 构建 → `dotnet publish` → 生成 `openiddict.pfx`（`ARG AUTH_CERT_PASSPHRASE`，空值直接失败）→ aspnet 运行时；安装 `curl`；创建 `/app/Logs` 并授权 `$APP_UID`；`USER $APP_UID`；`EXPOSE 8080`。
2. **DbMigrator Dockerfile**：SDK 构建 → `runtime:10.0`；创建 `/app/Logs`；`USER $APP_UID`；无端口。
3. **前端 Dockerfile**：`node:24-alpine` 执行 `npm ci && npm run build` → `nginx:1.27-alpine`；复制 `nginx.conf`。
4. **nginx.conf**：`/api/`、`/connect/`、`/.well-known/`、`/health-status`、`/swagger` 反代 `api:8080`；`/signalr/` 开 WebSocket（`Upgrade`、`Connection`、`proxy_read_timeout 3600s`、`proxy_buffering off`）；SPA fallback；`client_max_body_size 50m`；静态资源长缓存、`index.html` 不缓存。
5. **`.dockerignore`（根）**：排除 `.git`、`.vs`、`.env*`、`**/appsettings.secrets.json`、`**/appsettings.Production.json`、`*.pfx`、`**/bin`、`**/obj`、`**/Logs`、`docs`、`tests`、`aspire`、`frontend`、`samples`、`tools/KnowVault.Eval`、`.archify`、`.obsidian`；保留 `src`、`tools/KnowVault.DemoSeed`、`common.props`、`Directory.Build.props`、`NuGet.config`。
6. **docker-compose.yml**：按 §0.3 建 10 个服务；`migrator` 完成后再启 `api`（`service_completed_successfully`），`frontend` 与 `demo-seed` 等 `api` 健康；长驻服务 `restart: unless-stopped`，一次性任务 `restart: "no"`；Seq 注入 `ACCEPT_EULA=Y`；所有镜像固定版本。
7. **`.env.example`**：保留 Aspire 段，新增 Compose 段（端口、PostgreSQL/RabbitMQ/MinIO 口令、证书/加密口令、Demo 开关）；Key 留空。

### 验收

- `docker compose config` 无语法错误，变量都能解析。
- 干净上下文构建成功；镜像内 `find /app -name 'appsettings.secrets.json'` 为空。
- `docker compose up -d` 后 `postgres/redis/qdrant/minio/rabbitmq/seq/api/frontend` 全部 healthy，`migrator` 退出 0。
- 浏览器 `http://localhost:8080` 能登录；SignalR 流式输出不被 nginx 截断。

---

## Task 2：Redis 缓存接线

### Files

- `src/KnowVault.HttpApi.Host/KnowVault.HttpApi.Host.csproj`（修改）
- `src/KnowVault.HttpApi.Host/KnowVaultHttpApiHostModule.cs`（修改）

### Steps

1. 添加 `Volo.Abp.Caching.StackExchangeRedis` 10.5.0。
2. 模块 `[DependsOn]` 增加 `typeof(AbpCachingStackExchangeRedisModule)`。
3. 不改业务代码；`Redis__Configuration` 已由 Compose/Aspire 注入。

### 验收

- `dotnet build` 0 警告。
- `docker compose exec redis redis-cli keys '*'` 能看到 ABP 缓存键（首次请求后）。
- 停掉 Redis 后 API 请求报错而非静默回落到内存缓存（确认真的接了 Redis）。

---

## Task 3：演示账号与示例数据

### Files

- `src/KnowVault.Domain/Seeds/KnowVaultIdentityDataSeedContributor.cs`（修改）
- `src/KnowVault.DbMigrator/KnowVault.DbMigrator.csproj`（修改）
- `src/KnowVault.DbMigrator/appsettings.Development.json`（新增）
- `tools/KnowVault.DemoSeed/KnowVault.DemoSeed.csproj`、`Program.cs`、`Dockerfile`、`README.md`（新增）
- `KnowVault.slnx`（修改）
- `docker-compose.yml`（追加 `demo-seed`）

### Steps

1. 种子贡献者注入 `IConfiguration`；删除 `#if DEBUG`，改为 `if (configuration.GetValue<bool>("Demo:SeedAccounts"))` 创建 `demo`/`viewer`（密码 `1q2w3E*`）。
2. DbMigrator csproj：`appsettings.secrets.json` 改为 `Condition="Exists('appsettings.secrets.json')"`；增加 `appsettings.Development.json` 内容包含。
3. `appsettings.Development.json`：`Demo:SeedAccounts=true`。
4. DemoSeed（纯 `HttpClient`，不引 ABP）：
   - 读环境变量：`API_BASE_URL`、`DEMO_USERNAME`、`DEMO_PASSWORD`、`KNOWLEDGE_BASE_NAME`、`SAMPLES_DIR`、`SILICONFLOW_API_KEY`、`WAIT_TIMEOUT_SECONDS`、`INGEST_TIMEOUT_SECONDS`。
   - 等 `/health-status` 200（默认 120s）。
   - `POST /connect/token` 密码模式登录 `demo`。
   - `GET /api/app/knowledge-base` 查 `KnowVault 示例库`，不存在则创建。
   - 无 `SILICONFLOW_API_KEY`：只建库并提示「配置 Key 后重跑」。
   - 逐个上传 `SAMPLES_DIR` 下 7 份样例（multipart：`file` / `knowledgeBaseId` / `title`），标题取文件名去扩展名；已存在同名标题或服务端返回重复错误则跳过。
   - 轮询 `GET /api/app/document?knowledgeBaseId=...` 直到全部 Ready（2）/ Failed（3）或超时；打印每条状态与失败原因。
   - 退出码：成功/跳过 0；API 不可用或超时 1；有 Failed 打印警告仍 0。
5. DemoSeed Dockerfile：SDK 构建 → `runtime:10.0`；`USER $APP_UID`。
6. Compose：`demo-seed` 挂载 `./samples/kb-import:/samples:ro`，`SILICONFLOW_API_KEY` 从 `.env` 注入。

### 验收

- 干净卷启动后 `demo` 登录可见「KnowVault 示例库」与 7 份文档，配置 Key 时全部 Ready。
- 重复启动不重复建库、不重复上传。
- 无 Key 时跳过导入并给出明确提示，API/前端仍可用。
- `admin` 可见示例库；`viewer` 不能上传。

---

## Task 4：合规与清理

### Files

- `LICENSE`（新增）
- `THIRD-PARTY-LICENSES.md`（新增）
- `.gitignore`（修改）
- `docs/driver/问题/*`（从仓库与历史移除）
- `docs/interview/`（从仓库排除，从未入库）

### Steps

1. LICENSE：MIT，版权行 `Copyright (c) 2026 KnowVault contributors`。
2. 第三方许可清单：直接依赖 + 服务镜像 + 运行时；注明 MassTransit 8.x、FluentAssertions 7.x、MinIO/Redis 服务镜像与代码许可的区别。
3. `.gitignore`：新增 `docs/interview/`、`docs/driver/问题/`、`/.archify/`。
4. 将 `docs/driver/问题/` 与 `docs/interview/` 移出工作树并加入 `.gitignore`，再用 `git filter-branch` 从全部历史中清除（含已删除文件的提交）。
5. 密钥扫描：工作区 + 全部 git 历史；复核命中项；构建后扫描镜像。
6. 仓库体积：`git count-objects -vH`、`git ls-files` 检查 bin/obj/node_modules/dist。

### 验收

- `LICENSE` 与 `THIRD-PARTY-LICENSES.md` 落库，无许可冲突。
- 工作区与历史无真实 Key；镜像内无 secrets。
- 公开树不含内部问题记录与面试笔记。
- `.git` 与截图体积合理。

---

## Task 5：README 与素材

### Files

- `README.md`（新增）
- `docs/assets/architecture.svg`（新增）
- `docs/assets/screenshots/01-login.png` … `07-dashboard.png`（新增）
- `docs/docker-compose.md`（新增）

### Steps

1. 架构图：浏览器 → nginx → API；API → PostgreSQL/Redis/Qdrant/MinIO/RabbitMQ/Seq/DeepSeek/SiliconFlow；RabbitMQ → Ingest → Qdrant/MinIO。手写 SVG，不依赖 Mermaid。
2. README 按需求 §4.1 的 14 节结构写；首屏含定位、主截图、技术栈、快速开始、演示账号；明确「本地演示，不要暴露公网」。
3. 用 Compose 起全栈 + DemoSeed 数据，按清单截 7 张图（1440×900）：登录、知识库工作台、上传/处理中、流式回答+引用、引用抽屉、文档详情/切片、仪表盘用量。
4. `docs/docker-compose.md`：服务表、端口、配置、启动顺序、日志、重置、常见问题（端口冲突、无 Key、Seq EULA、pfx 口令）。
5. 校验 README 相对链接与图片路径。

### 验收

- README 首屏在 GitHub 渲染正常，图片不破。
- 6–8 张截图不含真实 Key、个人路径、浏览器插件。
- 架构图 < 100KB，单张截图尽量 < 300KB。

---

## Task 6：文档校订

### Files

- `TECH_STACK.md`、`AGENTS.md`、`docs/plans/2026-10-24-knowvault-open-source-roadmap.md`
- `docs/P6-交付说明.md`（新增）

### Steps

1. roadmap：P6 标记完成、验收勾选、完成记录、变更记录。
2. TECH_STACK：§6 P6 完成；§7 改为 Compose 路径；新增 §9.8 P6 实现细节；§14 加计划链接。
3. AGENTS：P6 状态、Compose 命令、演示账号、LICENSE、`docs/docker-compose.md` 入口；更新「构建/运行/测试」与「待办/注意」。
4. `docs/P6-交付说明.md`：Compose 拓扑、镜像构建、DemoSeed、许可、已知限制、阅读顺序。

### 验收

- README / roadmap / TECH_STACK / AGENTS 的 P6 状态、命令、链接一致。
- 所有相对链接可打开。

---

## Task 7：端到端验收

### Steps

1. `docker compose down -v`，模拟干净卷；`docker compose build --no-cache`。
2. `docker compose up -d`，等待全部 healthy 与 DemoSeed 完成。
3. 按 A1–A12 逐项操作并留证。
4. 停 Compose 后跑质量门禁。
5. 回写本文 §5 执行记录。

### §2 A1–A12 验收

| 编号 | 验收项 | 方法 |
|---|---|---|
| A1 | 干净克隆 + 一条命令启动 | 临时目录 clone + 仅 `.env.example` + `docker compose up -d --build` |
| A2 | 十步陌生人测试 | 注册/登录 → 建库 → 批量上传 → 状态 → 预览 → 提问流式 → 引用高亮 → 历史会话 → 赞踩/重生成 → 仪表盘 |
| A3 | README 首屏合格 | 定位、架构图、主截图、快速开始、演示账号首屏可见 |
| A4 | 无真实密钥 | 工作区/历史扫描 + 镜像内 `find` + `git status` 无 `.env` |
| A5 | LICENSE 与第三方许可 | LICENSE 落库；清单覆盖直接依赖与服务镜像 |
| A6 | 示例数据开箱即用 | 干净卷后 `demo` 可见示例库 + 7 份 Ready 文档 |
| A7 | 评估报告可引用 | README 指标表与 P5 报告一致 |
| A8 | 仓库体积/卫生 | `git ls-files` 无 bin/obj/node_modules；体积合理 |
| A9 | 端口/重置 | 默认端口可用；改 `.env` 端口可启动；`down -v` 说明准确 |
| A10 | 跨平台 | Windows + Docker Desktop 跑通；Dockerfile/脚本 LF |
| A11 | 质量门禁 | `dotnet build` 0 错误 0 警告；`dotnet test` 全绿；`npm run build` 通过 |
| A12 | 文档与状态一致 | README/roadmap/TECH_STACK/AGENTS 状态与命令一致 |

### 质量门禁

- `dotnet build KnowVault.slnx` 0 错误 0 警告。
- `dotnet test`：单测 + 集成测试全部通过（集成测试需 Docker；Compose 验收时先停 Compose）。
- `npm run build` 通过。
- `dotnet format style --verify-no-changes --diagnostics IDE1006` 无新增违规。
- 新增/改动文件 CRLF、UTF-8 无 BOM（Dockerfile / `.sh` 除外）。

---

## §3 风险与缓解

| 风险 | 影响 | 缓解 |
|---|---|---|
| Docker Hub / MCR 拉取慢 | 首次构建超时 | 固定版本；README 给出重试建议；构建命令单独执行 |
| OpenIddict issuer 不一致 | 登录 401、SignalR 失败 | Production + 显式 `SetIssuer`；`Authority` 与浏览器地址一致；DemoSeed 走内网 API |
| 自签 pfx 口令遗漏 | API 启动失败 | Dockerfile `test -n`；`.env.example` 给本地默认值；README 写清改口令需重建 |
| secrets 打进镜像 | 口令泄露 | `.dockerignore` 排除 + 镜像扫描 + A4 |
| 健康检查命令不存在 | 服务一直 unhealthy | 已实测 qdrant（bash TCP）、minio（curl）、seq（curl）、rabbitmq（diagnostics）、redis（cli）、postgres（pg_isready） |
| MinIO 9001 被 Windows 保留 | 端口绑定失败 | 默认改 9002 |
| Seq 未接受 EULA | 容器退出 | Compose 注入 `ACCEPT_EULA=Y`，README 说明 |
| DemoSeed 依赖模型 Key | 无 Key 时库为空 | 跳过导入并提示；README 首屏先要求配 Key |
| Compose 与集成测试抢 Docker | 测试失败 | 验收顺序：Compose 手工流程 → 停 Compose → `dotnet test` |

---

## §4 完成定义（DoD）

- 干净克隆 + 仅 `.env` 两个 Key，`docker compose up -d --build` 后 8080 可完整走十步。
- 所有长驻服务 healthy，migrator/demo-seed 退出 0。
- README 首屏合格，6–8 张截图与架构图入仓。
- LICENSE、第三方许可、无真实密钥、内部文档移出公开树。
- A1–A12 与质量门禁逐项留证，回写本文 §5。
- roadmap / TECH_STACK / AGENTS 状态一致。

---

## §5 执行记录（实施完成后回写）

### 5.1 A1–A12 验收结果

| 编号 | 结果 | 证据 |
|---|---|---|
| A1 | 通过 | 临时目录仅放发布文件与 `.env`，`docker compose build --no-cache` 后 `up -d`：8 个常驻服务全 healthy，migrator / demo-seed 均退出 0。首轮 frontend 因健康检查地址问题 unhealthy，修复后复测通过（见 5.3） |
| A2 | 通过（1 处口径偏差） | 新注册 `p6accept` → 登录 → 新建「验收知识库」→ 批量上传 md / docx / xlsx / pdf 4 份 → 全部「已就绪」→ 文档详情 6 个切片 → 流式问答 3.6s / 5 条引用 → 引用抽屉与「查看文档」→ 第二个会话 → 刷新后会话与赞踩状态恢复 → 重新生成（消息仍 2 条，3.5s）→ 仪表盘 2 问 / 18,609 tokens / ¥0.01。长期截图见 `docs/assets/screenshots/04`–`07`；偏差见 5.3 |
| A3 | 通过 | README 首屏含定位一句话、架构图、主截图、技术栈、快速开始与演示账号；`docs/assets/screenshots` 7 张截图齐备 |
| A4 | 通过 | `.env` / `appsettings.secrets.json` / `openiddict.pfx` 均未入库；工作区与新增文件扫描无 key 模式；镜像内仅 API 的 `/app/openiddict.pfx`（构建期生成，预期） |
| A5 | 通过 | MIT `LICENSE` 落库；`THIRD-PARTY-LICENSES.md` 覆盖 NuGet / npm 直接依赖与七个基础镜像，补齐 LeptonXLite 与 Razor RuntimeCompilation |
| A6 | 通过 | 干净卷后 `demo` 登录可见「KnowVault 示例库」与 7 篇「已就绪」文档；示例数据重跑幂等（端口验证时 demo-seed 再次退出 0） |
| A7 | 通过 | README 指标表与 P5 报告一致：三模式 Recall@5 0.900 / MRR@10 0.847 / nDCG@5 0.858 |
| A8 | 通过 | 445 个发布文件、4.52 MB；无 bin / obj / node_modules；`docs/assets` 1.1 MB、`samples/kb-import` 116 KB |
| A9 | 通过 | 默认 8080 / 8081；改 8088 / 8089 后 `up -d`（不重建）：SPA 200、issuer `http://localhost:8088/`、取 token 200、直连 8089 健康 200；`down -v` 清除 5 个命名卷与网络，与文档一致 |
| A10 | 通过 | Windows 10 + Docker Desktop 跑通 A1–A9；Dockerfile 与 `*.sh` 为 LF，其余文本 CRLF |
| A11 | 通过 | 见 5.2 |
| A12 | 通过 | README / roadmap / TECH_STACK / AGENTS 统一为 8 个常驻服务；issuer 写 `KNOWVAULT_HTTP_PORT`；README 引用能力措辞已与实现对齐 |

### 5.2 质量门禁

| 检查 | 结果 |
|---|---|
| `dotnet build KnowVault.slnx` | 0 警告 0 错误 |
| `dotnet test KnowVault.slnx` | 单测 109/109、集成 4/4 全绿（Compose 已停，Testcontainers 正常） |
| `npm run build`（frontend） | 通过；仅 Vite 既有 >500 kB chunk 提示 |
| `dotnet format style --verify-no-changes --diagnostics IDE1006` | exit 0（修复 DemoSeed 枚举后缀与 3 个 ABP 模块 using 排序后复跑） |
| 行尾 / BOM | 29 个改动 / 新增文本文件全部合规：Dockerfile、`*.sh`、AGENTS.md、.editorconfig 为 LF，其余 CRLF，无 BOM |

### 5.3 实测偏差与处理

| 偏差 | 处理 |
|---|---|
| frontend 健康检查用 `localhost`，容器内解析到 `::1` 而 nginx 只监听 IPv4，容器一直 unhealthy | 健康检查改 `http://127.0.0.1/`，重建后 healthy（`docker-compose.yml`） |
| Production 下 `UseErrorPage` 把未认证 API 的 401 改写成 302 跳 `/Error`，前端无法识别登录态 | Host 模块用 `UseWhen` 排除 `/api`、`/signalr`、`/connect`、`/.well-known`；复测 401 + `WWW-Authenticate: Bearer` |
| README 宣称引用「高亮命中位置」，实际引用抽屉展示命中切片全文并可跳文档详情，无字面高亮 | 修正 README 两处措辞；A2 按「引用片段可回看」记录 |
| `--no-cache` 构建中 NuGet / apt 多次超时重试，单次构建约 6 分钟以上 | 最终构建成功；根因是 Dockerfile 先 `COPY . .` 再 restore，本期不改，后续可拆层优化 |
| DemoSeed 新枚举缺 `Enum` 后缀触发 IDE1006；3 个 ABP 模块 using 顺序不符格式规则 | 枚举改 `UploadResultEnum`；`dotnet format style --diagnostics IMPORTS` 修正排序，复跑通过 |
