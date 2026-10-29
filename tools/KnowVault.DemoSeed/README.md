# KnowVault.DemoSeed

一次性导入演示数据的控制台工具：登录 demo 账号 → 找到或创建「KnowVault 示例库」→ 上传 `samples/kb-import/` 下的 7 份样例文档 → 轮询直到解析与向量化完成。

只调用公开 HTTP API，不引用 ABP 程序集，也不直接访问数据库、Qdrant 或 MinIO。

## 运行方式

Docker Compose 会在 `api` 健康后自动运行：

```bash
docker compose logs -f demo-seed
```

单独重跑（幂等，已存在的知识库与文档会跳过）：

```bash
docker compose run --rm demo-seed
```

本机直接运行（API 需已启动，默认 `http://localhost:8081`）：

```bash
dotnet run --project tools/KnowVault.DemoSeed
```

## 环境变量

| 变量 | 默认值 | 说明 |
|---|---|---|
| `API_BASE_URL` | `http://localhost:8081` | API 基地址；Compose 内为 `http://api:8080` |
| `DEMO_USERNAME` | `demo` | 登录账号 |
| `DEMO_PASSWORD` | `1q2w3E*` | 登录口令（仅本地演示） |
| `KNOWLEDGE_BASE_NAME` | `KnowVault 示例库` | 目标知识库名称 |
| `SAMPLES_DIR` | `/samples` | 样例文档目录 |
| `SILICONFLOW_API_KEY` | 空 | 为空时只建库、跳过文档导入 |
| `WAIT_TIMEOUT_SECONDS` | `120` | 等待 API 健康的秒数 |
| `INGEST_TIMEOUT_SECONDS` | `600` | 等待文档处理完成的秒数 |

## 退出码

- `0`：全部导入成功、全部跳过，或建库成功但未配置 Key。
- `1`：API 不可用、登录失败、建库失败或等待处理超时。

有文档处理失败时只打印警告并返回 `0`，不阻塞 `docker compose up`。
