# P5 检索评估

本目录是 KnowVault 检索质量的固定评估资产：6 篇语料、30 道金标题、评估工具与历史报告。

评估只回答一个问题：**混合检索与查询改写到底有没有把答案找得更准**。指标用于同一份语料下的横向比较，不代表系统在任意业务数据上的绝对水平。

## 目录结构

```
docs/eval/
├─ corpus/                 6 篇评测语料（Markdown，上传到评估知识库）
├─ golden-set.json         30 道题：关键词 / 语义 / 跨文档 / 不可回答
├─ README.md               本文件
└─ reports/                历次评估报告（Markdown + 同名 JSON）
tools/KnowVault.Eval/      评估工具（控制台项目）
```

## 三种模式

| 模式 | 检索方式 | 生成 | 用途 |
|---|---|---|---|
| `vector` | 纯向量召回（dense） | 可选 | 基线 A |
| `hybrid` | dense + sparse 关键词，加权 RRF 融合 | 可选 | 验证 B 对 A |
| `hybrid-rewrite` | 混合检索前先做查询改写 | 可选 | 验证 C 对 B |

三种模式都开启重排（候选 20 → 最终 topK 5）。`--retrieval-only` 只跑检索，不调用 DeepSeek，引用指标与弃答率不参与比较。

## 指标口径

- **Recall@5**：topK 中命中的金标切片数 / 该题全部金标切片数（可回答题）。
- **MRR@10**：第一个金标命中的倒数排名；由于 `RagService` 只返回 topK 条，实际上限为 topK。
- **nDCG@5**：二元相关性折损累计后按理想排序归一化。
- **引用命中率**：回答引用的切片中至少含一条金标的题占比（有引用的可回答题）。
- **引用精确率**：回答引用的切片中属于金标的比例。
- **弃答率**：不可回答题中回答明确表示资料不足的比例。

金标以「锚点字符串」定位：题目的 `anchor` 必须能在对应语料的某个切片正文里找到，评估工具在开跑前会滚动 Qdrant 全量切片做校验，任何锚点失配都会直接报错并列出题号。

## 前置条件

1. 已通过 Aspire 或手工方式启动 Qdrant、PostgreSQL、MinIO、RabbitMQ。
2. 新建一个**专用于评估**的知识库，把 `corpus/` 下 6 篇 Markdown 全部上传并等待处理为 Ready。
3. 环境变量就绪：

   ```bash
   export Ai__SiliconFlowApiKey=...   # embedding 与 rerank 必需
   export Ai__DeepSeekApiKey=...      # 生成与改写需要；只跑检索可不配
   ```

   Key 只从环境变量读取，不写入仓库与报告。

## 运行

```bash
# 全量三模式（30 题，含生成）
dotnet run --project tools/KnowVault.Eval -- \
  --knowledge-base-id <评估知识库 GUID> \
  --modes vector,hybrid,hybrid-rewrite

# 只跑检索，不调用 DeepSeek
dotnet run --project tools/KnowVault.Eval -- \
  --knowledge-base-id <评估知识库 GUID> \
  --retrieval-only

# 调试：只跑前 5 题
dotnet run --project tools/KnowVault.Eval -- \
  --knowledge-base-id <评估知识库 GUID> \
  --question-limit 5
```

常用参数：

| 参数 | 说明 | 默认 |
|---|---|---|
| `--knowledge-base-id` | 评估知识库 GUID（必填） | — |
| `--modes` | `vector,hybrid,hybrid-rewrite` 的任意组合 | 全部 |
| `--top-k` | 最终引用条数（1–8） | 5 |
| `--delay-ms` | 题目之间等待毫秒数 | 500 |
| `--retrieval-only` | 只跑检索 | 关闭 |
| `--question-limit` | 只跑前 N 题 | 全量 |
| `--output` | Markdown 报告路径 | `docs/eval/reports/<日期>-p5-eval.md` |

报告与同名 JSON 一起写入 `docs/eval/reports/`；报告记录代码提交、语料 SHA256、关键配置、逐题结果与失败案例，便于复现。

## 语料与题目设计

- 6 篇语料覆盖 RAG 基础、切片、向量检索、混合检索、重排、评估指标六个主题，单篇约 800–950 字符，保证每篇至少切成 2 片。
- 语料内埋了少量编号（如 `KV-RRF-60`、`KV-VEC-1024`），用于构造关键词题；语义题不复述原文措辞；跨文档题需要两篇以上语料共同支撑；不可回答题的主题在语料中完全不存在。
- 金标锚点是语料中的一段连续文本，不以行号或切片序号定位，避免重新切片后失效。

## 已知限制

- 30 题、6 篇语料是小样本，单题波动会放大到平均值；结论只用于判断方向。
- 生成类指标受模型版本与网络状态影响；检索类指标在配置不变时可重复。
- `RagService` 只返回 topK 条，MRR@10 实际被 topK 截断。
- 查询改写开启后，DeepSeek 对同一问题的改写结果可能不同，C 模式存在一定随机性。
