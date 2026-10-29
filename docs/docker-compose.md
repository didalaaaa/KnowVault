# Docker Compose 部署与演示指南

本文件说明 KnowVault 的 Compose 拓扑、配置项、启动顺序、日志与常见问题。快速开始见根目录 [README](../README.md)。

## 1. 前置条件

- Docker Desktop（Windows/macOS）或 Docker Engine 24+ 与 Compose v2
- 至少 8 GB 可用内存、10 GB 磁盘
- 可访问 Docker Hub、MCR 与模型厂商 API
- 宿主机端口 `8080`、`8081`、`5341`、`6333`、`9002`、`15672` 可用（均可覆盖）

## 2. 服务拓扑

```text
frontend (nginx, 8080)
  ├─ /            → Vue 3 构建产物
  ├─ /api/        → api:8080
  ├─ /connect/    → api:8080（OpenIddict）
  ├─ /.well-known/→ api:8080
  └─ /signalr/    → api:8080（WebSocket）

api (Production, :8080)
  ├─ postgres:5432
  ├─ redis:6379
  ├─ qdrant:6334 (gRPC)
  ├─ minio:9000
  ├─ rabbitmq:5672
  ├─ seq:5341
  ├─ DeepSeek API（生成）
  └─ SiliconFlow API（Embedding + Rerank）

migrator    一次性：迁移 + 种子（admin / demo / viewer / 默认库 / OpenIddict 客户端）
demo-seed   一次性：demo 登录 → 建示例库 → 上传 7 份样例 → 轮询处理状态
```

## 3. 端口与卷

| 服务 | 容器端口 | 宿主端口（默认） | 数据卷 | 健康检查 |
|---|---|---|---|---|
| `frontend` | 80 | `8080` | — | `wget --spider /` |
| `api` | 8080 | `8081` | — | `curl /health-status` |
| `migrator` | — | — | — | 退出码 0 |
| `demo-seed` | — | — | 挂载 `./samples/kb-import` | 退出码 0 |
| `postgres` | 5432 | 不暴露 | `knowvault-postgres` | `pg_isready` |
| `redis` | 6379 | 不暴露 | `knowvault-redis` | `redis-cli ping` |
| `qdrant` | 6333 / 6334 | `6333` | `knowvault-qdrant` | TCP 探测 6333 |
| `minio` | 9000 / 9001 | `9002` → 9001 | `knowvault-minio` | `/minio/health/live` |
| `rabbitmq` | 5672 / 15672 | `15672` | `knowvault-rabbitmq` | `rabbitmq-diagnostics -q ping` |
| `seq` | 80 / 5341 | `5341` → 80 | `knowvault-seq` | `/health` |

MinIO 控制台默认映射到 `9002`：部分 Windows 机器把 `9001` 列入系统保留端口段，绑定会失败。

## 4. 配置

所有变量集中在根目录 `.env`，模板为 `.env.example`。Compose 段的关键变量：

| 变量 | 默认 | 说明 |
|---|---|---|
| `KNOWVAULT_HTTP_PORT` | `8080` | 浏览器访问端口，同时决定 OpenIddict issuer |
| `KNOWVAULT_API_PORT` | `8081` | API 直连端口 |
| `POSTGRES_PASSWORD` | `knowvault-local-password` | 仅容器内使用 |
| `RABBITMQ_DEFAULT_USER` / `PASS` | `knowvault` / `knowvault-local-password` | 不使用 `guest` |
| `MINIO_ROOT_USER` / `PASSWORD` | `minioadmin` / `minioadmin` | 仅本地演示 |
| `AUTH_CERT_PASSPHRASE` | `knowvault-local-cert-change-me` | OpenIddict 证书口令 |
| `STRING_ENCRYPTION_PASSPHRASE` | `knowvault-local-encryption-change-me` | ABP 字符串加密口令 |
| `DEEPSEEK_API_KEY` | 空 | 生成模型 Key |
| `SILICONFLOW_API_KEY` | 空 | Embedding + Rerank Key |
| `DEMO_SEED_ACCOUNTS` | `true` | 是否种演示账号 |

模型名、检索、切片与定价参数在 `src/KnowVault.HttpApi.Host/appsettings.json`；Compose 通过 `Ai__*` 覆盖 Key、BaseUrl 与模型名。

### 证书与 issuer

- API 镜像在**构建期**用 `AUTH_CERT_PASSPHRASE` 生成 `openiddict.pfx`，运行期读取同一口令。
- issuer 固定为 `http://localhost:${KNOWVAULT_HTTP_PORT}`。修改端口后，浏览器必须使用新端口访问；不要用 `127.0.0.1` 与 `localhost` 混用。
- 修改 `AUTH_CERT_PASSPHRASE` 后必须重建 API 镜像：

```bash
docker compose build api
docker compose up -d api
```

## 5. 启动顺序

Compose 通过健康检查与完成条件控制顺序：

```text
postgres/redis/qdrant/minio/rabbitmq 健康
        ↓
migrator 完成（迁移 + 种子）
        ↓
api 健康
        ↓
frontend 可用
demo-seed 导入示例数据
```

查看状态与日志：

```bash
docker compose ps
docker compose logs -f migrator
docker compose logs -f api
docker compose logs -f demo-seed
```

首次启动受镜像拉取与构建影响，通常 3–10 分钟。`demo-seed` 完成后容器退出是正常现象。

## 6. 日常命令

```bash
# 启动 / 重建
docker compose up -d
docker compose up -d --build

# 只看某个服务
docker compose logs -f api
docker compose restart api

# 重新导入示例数据（幂等）
docker compose run --rm demo-seed

# 停止（保留数据）
docker compose down

# 停止并删除数据卷（会清空数据库、向量、原件、日志）
docker compose down -v
```

直接调试 API：

```bash
curl http://localhost:8081/health-status
open http://localhost:8081/swagger
```

查看数据库：

```bash
docker compose exec postgres psql -U postgres -d KnowVault
```

## 7. 可观测

- Seq UI：<http://localhost:5341>。API 与 Migrator 的 Serilog 日志、OTLP Trace 都会进入 Seq。
- Qdrant Dashboard：<http://localhost:6333/dashboard>。
- RabbitMQ 管理台：<http://localhost:15672>，账号见 `.env`。
- MinIO 控制台：<http://localhost:9002>，账号见 `.env`。

## 8. 常见问题

### 端口被占用

修改 `.env` 中对应端口后重新 `docker compose up -d`。`KNOWVAULT_HTTP_PORT` 同时影响 issuer，修改后必须用新端口访问前端。

### Seq 容器启动即退出

Seq 2025.1 需要接受 EULA；Compose 已设置 `ACCEPT_EULA=Y`。若仍退出，检查 `docker compose logs seq`。Seq 单用户免费，商用请阅读官方条款。

### 登录 401 或 SignalR 连接失败

- 确认浏览器地址端口与 `KNOWVAULT_HTTP_PORT` 一致。
- 不要混用 `localhost` 与 `127.0.0.1`：令牌 issuer 是 `http://localhost:<端口>`。
- 检查 `docker compose logs api` 是否有证书口令或 issuer 报错。

### 示例库为空

- 未配置 `SILICONFLOW_API_KEY` 时 `demo-seed` 只建库、不导入文档；补 Key 后执行 `docker compose run --rm demo-seed`。
- 文档一直 Pending/Processing：查看 `docker compose logs api` 与 Seq，确认 RabbitMQ、Qdrant、MinIO 健康。

### 文档处理失败

常见原因是模型 Key 无效、额度不足或网络不可达。失败文档会保留在列表并显示原因，可在页面重试或删除；不影响其它文档。

### 修改旧数据卷后启动报 `VectorSchemaMismatchException`

P4 的 Qdrant collection 没有稀疏向量。用 Admin 调用 `POST /api/app/maintenance/rebuild-vector-index` 重建，等待文档重新处理完成。全新卷不需要。

### 内存不足

Compose 默认同时运行 8 个常驻服务。可在 Docker Desktop 提高内存上限，或按需停掉不参与演示的服务（例如 `docker compose stop seq`）。

## 9. 安全说明

- 本 Compose 面向**本地演示**：HTTP、默认口令、演示账号均不适合公网。
- 正式环境至少需要：HTTPS、强口令、关闭 `DEMO_SEED_ACCOUNTS`、限制管理台端口、备份数据卷。
- `.env`、`appsettings.secrets.json` 与仓库内的 `openiddict.pfx` 均被 `.gitignore` / `.dockerignore` 排除，不会进入仓库或构建上下文；API 镜像会在构建期用 `AUTH_CERT_PASSPHRASE` 现场生成一份自签证书，仅用于本地 issuer。
- 若需把服务暴露给其他机器，请同时调整 `App__SelfUrl`、`AuthServer__Authority`、`App__CorsOrigins` 为实际访问地址；当前 Compose 模板固定为 `localhost`。
