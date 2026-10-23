# KnowVault 技术选型常见问题

本文档以问答形式记录 KnowVault 关键技术决策的理由。每条回答都给出了可验证的具体版本或数字，便于测试检索是否命中了正确片段。

## 为什么生成模型用 DeepSeek 而向量化用 Qwen

因为 DeepSeek 官方没有稳定提供嵌入与重排能力，如果三个环节全部依赖同一家厂商，检索质量就没有保障。KnowVault 采取跨厂商混合路由：回答生成用 DeepSeek 的 deepseek-chat，理由是中文效果好且价格便宜；向量化用阿里云百炼 Qwen 的 text-embedding-v3；重排用 Qwen 的 rerank 系列。三个环节统一封装成 OpenAI 兼容调用层，方便日后更换厂商而不改动业务代码。

## 为什么向量库只用一个 Qdrant collection

因为按知识库各建一个 collection 会导致 collection 数量随业务增长无限膨胀，运维和初始化都不可控。KnowVault 的做法是单一 collection，名字叫 knowvault_chunks，向量维度是 1024，距离函数是余弦相似度。区分不同知识库依靠 payload 过滤，而不是物理拆分。为每个向量必须携带 tenantId、knowledgeBaseId、documentId、chunkIndex 四个过滤字段，另外还要带 content、documentTitle、headingPath、sourceType 用于引用溯源。

## 为什么单租户运行时 tenantId 要归一化成 Guid.Empty

因为 Qdrant 无法对空值做匹配过滤，如果 payload 里的 tenantId 是 null，那么按租户过滤的条件永远不命中，检索结果恒为空。所以即使当前只跑一个租户，写入时也要把 null 归一化成 Guid.Empty。这是本项目最容易踩的一个坑。

## 为什么 MassTransit 必须钉在 8.5.7

因为 9.x 换了许可协议。9.2.3 的 nuspec 里没有 license 表达式，只有一个指向 massient 商业授权的 licenseUrl，属于商业许可产品。8.5.7 仍是 Apache 2.0 许可，并且带有 net10 目标框架组，因此禁止升级到 9.x。

## 为什么 FluentAssertions 必须钉在 7.x

同理属于许可问题。7.2.0 是 Apache 2.0，而 8.11.0 已经改为 Xceed 的商业最终用户许可协议。测试框架本身用 xUnit，集成测试用 Testcontainers 起真实的 Postgres、RabbitMQ、MinIO 与 Qdrant 容器。

## 为什么 PDF 解析用 PdfPig 而不是 iTextSharp

因为 iTextSharp 是 AGPL 许可，会污染整个项目的开源许可，属于明确禁止引入的依赖。KnowVault 为每种格式各选一个专用库：PDF 用 PdfPig，Word 用 DocumentFormat OpenXml，Excel 用 ClosedXML 并已弃用 NPOI，Markdown 用 Markdig，HTML 用 HtmlAgilityPack。

## 为什么 MinIO 的 AGPL 许可不会传染本项目

因为仓库只通过 Docker 运行未修改的 MinIO 官方镜像，并且使用自己 Apache 2.0 许可的 SDK 去调用它，没有修改服务端源码，也不构成对仓库代码的许可传染。同理，SignalR 服务端在 .NET 10 共享框架内，零 NuGet 包依赖；前端使用的 microsoft signalr 客户端库是 MIT 许可。

## 为什么切片长度用字符数而不是 token 数

因为中文场景下没有可靠的 .NET 分词器，为了算 token 而引入 tiktoken 类依赖成本过高。P1 用字符数近似 token，默认切片长度 1000 字符、相邻切片重叠 120 字符，量级大约相当于 600 token 与 70 token。重叠长度被限制为不超过切片长度的一半，否则切片会互相吞没。当单个段落超长时按句子边界硬切，此时不再叠加重叠段。

## 为什么 Qdrant 连接用 6334 端口

因为官方 Qdrant 客户端走 gRPC，gRPC 端口是 6334，而 6333 是 REST 端口，两者不能混用。这是排查向量库连不上时第一个要确认的配置。
