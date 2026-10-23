# KnowVault SiliconFlow 模型切换计划（2026-10-23）

> 需求来源：`docs/driver/需求/需求分析-1023.md`（更换向量模型、更换重排模型，均参考 SiliconFlow API 文档）
> 范围：只替换 **Embedding + Rerank** 的供应商与实现；生成仍走 DeepSeek，RAG 编排、Qdrant 结构、前端均不改
> 结论：Qwen / DashScope 的代码与配置**直接移除**，不保留双实现

---

## 1. 决策

| # | 决策 | 理由 |
|---|---|---|
| D1 | 只接 SiliconFlow，不保留 Qwen 切换开关 | 换 embedding 供应商必须重新向量化全部历史数据，双实现没有实际收益，只会留下死代码 |
| D2 | Embedding 用 `BAAI/bge-m3` | 输出 1024 维，与现有 Qdrant collection（`knowvault_chunks`，1024 维 Cosine）一致，**不需要改向量库结构** |
| D3 | Rerank 用 `BAAI/bge-reranker-v2-m3` | SiliconFlow 官方重排模型，与 Embedding 共用 BaseUrl 和 API Key |
| D4 | 不做数据迁移 | 切换时历史文档已删除；代码不会自动清理 Qdrant，今后换 embedding 模型必须重新上传/重新向量化 |
| D5 | 只换 rerank 模型不需要重新向量化 | 重排发生在检索之后，不参与向量写入 |

## 2. 接口形态

两个端点共用 `Ai:SiliconFlowBaseUrl`（默认 `https://api.siliconflow.cn/v1`）与同一个 Bearer Key：

| 能力 | 端点 | 关键请求字段 | 关键响应字段 |
|---|---|---|---|
| Embedding | `POST {base}/embeddings` | `model` / `input`（数组）/ `encoding_format=float`；**不传 `dimensions`** | `data[].index` / `data[].embedding`（OpenAI 兼容） |
| Rerank | `POST {base}/rerank` | `model` / `query` / `documents` / `top_n`（传候选全量）/ `return_documents=false` | 顶层 `results[].index` / `results[].relevance_score` |

要点：

- `bge-m3` 不支持 `dimensions` 参数，传了会返回 400；1024 维是模型固定输出。
- Rerank 的服务端 `top_n` 只用于控制返回条数，最终保留几条由 `RagService` 本地 `Take(topK)` 裁剪（沿用 P2 策略）。
- Embedding 仍按每批 10 条保守分批（SiliconFlow 单请求上限 32 条），并按响应 `index` 还原顺序。

## 3. 文件级改动

| 类型 | 文件 | 改动 |
|---|---|---|
| 配置模型 | `src/KnowVault.Ai/Configuration/AiOptions.cs` | 删除 `Qwen*` 配置；新增 `SiliconFlowApiKey` / `SiliconFlowBaseUrl` / `SiliconFlowEmbeddingModel` / `SiliconFlowRerankModel` |
| 实现（改名） | `src/KnowVault.Ai/Embeddings/SiliconFlowEmbeddingService.cs` | 由 `QwenEmbeddingService` 改名；请求改走 SiliconFlow `/embeddings`，异常/日志文案同步 |
| 实现（改名） | `src/KnowVault.Ai/Rerank/SiliconFlowRerankService.cs` | 由 `QwenRerankService` 改名；改走 OpenAI 兼容 `/rerank`，删除 DashScope 原生请求体与「200 带 code」兜底 |
| 解析（改名） | `src/KnowVault.Ai/Rerank/SiliconFlowRerankResponseParser.cs` | 由 `QwenRerankResponseParser` 改名；解析顶层 `results`，按分数降序 |
| 测试（改名） | `tests/KnowVault.Tests/SiliconFlowRerankResponseParserTests.cs` | 类名与样例 JSON 改为 SiliconFlow 格式 |
| 装配 | `src/KnowVault.Ai/KnowVaultAiModule.cs`、`src/KnowVault.Ai/KnowVault.Ai.csproj`、`src/KnowVault.Domain/Embeddings/IEmbeddingService.cs` | 注释与命名同步；ABP 约定注册不变（类名仍以 `EmbeddingService` / `RerankService` 结尾） |
| 宿主配置 | `src/KnowVault.HttpApi.Host/appsettings.json` | `Ai` 节改为 4 个 `SiliconFlow*` 键；`Qdrant:VectorSize` 保持 1024 |
| 本地编排 | `aspire/KnowVault.AppHost/Program.cs` | `.env` 的 `SILICONFLOW_*` 映射为 `Ai__SiliconFlow*`；删除 Qwen 映射 |
| 环境变量样例 | `.env.example` | Qwen 段替换为 `SILICONFLOW_API_KEY` / `SILICONFLOW_BASE_URL` / `SILICONFLOW_EMBEDDING_MODEL` / `SILICONFLOW_RERANK_MODEL` |
| 集成测试替身 | `tests/KnowVault.IntegrationTests/FakeEmbeddingService.cs` | 仅注释同步，仍返回固定 1024 维向量 |
| 文档 | `TECH_STACK.md`、`AGENTS.md`、`docs/P1-代码导读.md`、`docs/P2-代码导读.md` | 模型、端点、配置键与文件链接同步 |

不改：`RagService`、`DocumentProcessor`、Qdrant collection 定义、前端、数据库迁移（Qwen 的 `Ai__*` 配置不落库）。

## 4. 验收

- [x] `dotnet build KnowVault.slnx`：0 错误 0 警告
- [x] `dotnet test tests/KnowVault.Tests`：33/33 通过
- [ ] 真实端到端（需本地 `.env` 填入 `SILICONFLOW_API_KEY`）：上传文档 → 状态 `Ready`，Qdrant 中向量为 1024 维
- [ ] 真实提问：日志出现 `SiliconFlow rerank 完成`，citations 分数来自 `relevance_score`
- [ ] 负向：把 `SILICONFLOW_API_KEY` 改错 → 上传文档落 `Failed`，错误信息包含 HTTP 状态码

> 未填 Key 时只能验证构建与单测；集成测试使用 `FakeEmbeddingService`，不会真实调用 SiliconFlow。

## 5. 风险与注意

| 风险 | 处理 |
|---|---|
| `bge-m3` 实际维度与 1024 不符 | 首次真实上传时检查 Qdrant upsert 是否报维度错误；若不符需同步 `Qdrant:VectorSize` 并重建 collection |
| SiliconFlow 免费额度/限流 | 批量向量化每批 10 条；失败时文档落 `Failed`，可用 P3 的「重新处理」重试 |
| 模型名变更或下线 | 模型名全部走配置（`.env` 可覆盖），不硬编码在代码里 |
| 误以为换回 Qwen 无需成本 | 换 embedding 必须重新向量化；本计划不保留 Qwen 实现 |
