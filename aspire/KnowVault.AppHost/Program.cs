using Aspire.Hosting.ApplicationModel;

var builder = DistributedApplication.CreateBuilder(args);

// 环境变量读取：模型 Key 与 MinIO 口令一律来自本地 .env / 进程环境，严禁写进仓库
static string? Env(string name) => Environment.GetEnvironmentVariable(name);

// ---------- 依赖服务 ----------

// PostgreSQL：主库（ABP + EF Core）
var postgres = builder.AddPostgres("postgres")
    .WithImageTag("16")
    .WithDataVolume("kv-postgres-data");

// 创建 KnowVault 数据库；把连接字符串注入到 ABP 的 ConnectionStrings:Default
var database = postgres.AddDatabase("KnowVault");

// Redis：ABP 缓存
var redis = builder.AddRedis("redis")
    .WithDataVolume("kv-redis-data");

// Qdrant：向量库（单一 collection + payload 过滤）
var qdrant = builder.AddQdrant("qdrant")
    .WithImage("qdrant/qdrant")
    .WithDataVolume("kv-qdrant-data");

// RabbitMQ：文档摄入队列（P1 为进程内 consumer，消息仍走 MQ）
var rabbitmq = builder.AddRabbitMQ("rabbitmq")
    .WithImage("rabbitmq:3-management")
    .WithDataVolume("kv-rabbitmq-data");

// Seq：日志可视化（全链路接入留 P3，此处先起服务）
var seq = builder.AddSeq("seq");

// MinIO：S3 兼容对象存储，存原始文档。
// 官方镜像必须显式给 server 子命令，否则容器起不来；控制台端口 9001。
var minio = builder.AddContainer("minio", "minio/minio", "latest")
    .WithArgs("server", "/data", "--console-address", ":9001")
    .WithEnvironment("MINIO_ROOT_USER", Env("MINIO_ROOT_USER") ?? "minioadmin")
    .WithEnvironment("MINIO_ROOT_PASSWORD", Env("MINIO_ROOT_PASSWORD") ?? "minioadmin")
    .WithVolume("kv-minio-data", "/data")
    .WithEndpoint(9000, 9000, "http")   // S3 API：宿主 9000，与注入给 API 的 Minio__Endpoint 对齐
    // 控制台：宿主端口动态分配 —— 本机 9001 落在 Windows 的 TCP 保留端口段（netsh 可见），钉死会导致容器发布端口失败
    .WithEndpoint(targetPort: 9001, name: "console");

// ---------- 应用 ----------

// 迁移 + 种子（含默认知识库）：先跑完再启动 API，避免 API 起来时表还不存在。
// ⚠️ 不要写 WithReference(database, "ConnectionStrings:Default")：Aspire 会自己加 ConnectionStrings__ 前缀，
//    结果注入成 ConnectionStrings__ConnectionStrings:Default，ABP 读不到就回落到 appsettings 的 localhost:5432
//    （实测会连到宿主机上别的 Postgres 并报 28P01）。这里显式写 ABP 真正读取的配置键。
var dbMigrator = builder.AddProject("knowvault-dbmigrator", "../../src/KnowVault.DbMigrator/KnowVault.DbMigrator.csproj")
    .WithEnvironment("ConnectionStrings__Default", database)
    .WaitFor(database);

var api = builder.AddProject("knowvault-api", "../../src/KnowVault.HttpApi.Host/KnowVault.HttpApi.Host.csproj")
    .WaitForCompletion(dbMigrator)
    // 端口必须与 appsettings 的 App:SelfUrl / AuthServer:Authority 一致，
    // 否则 OpenIddict 的 issuer 与前端拿到的 authority 不一致，令牌校验必然失败
    .WithHttpsEndpoint(port: 44300, name: "https")
    .WithEnvironment("ConnectionStrings__Default", database)
    // ABP 读的是 Redis:Configuration（不是 ConnectionStrings:redis），否则会退回内存缓存
    .WithEnvironment("Redis__Configuration", redis)
    .WithReference(rabbitmq)          // MassTransit 用 GetConnectionString("rabbitmq")，这个键名是对的
    .WithEnvironment("App:CorsOrigins", "http://localhost:5173")
    // QdrantVectorStore 走 gRPC，必须注入 6334 的 grpc 端点（不是 6333 的 REST 端点）
    .WithEnvironment("Qdrant__Endpoint", qdrant.GetEndpoint("grpc"))
    // MinIO 容器端口由 DCP 代理，宿主端口是动态的 → 必须注入 HostAndPort，写死 localhost:9000 会连不上
    .WithEnvironment("Minio__Endpoint", minio.GetEndpoint("http").Property(EndpointProperty.HostAndPort))
    .WithEnvironment("Minio__AccessKey", Env("MINIO_ROOT_USER") ?? "minioadmin")
    .WithEnvironment("Minio__SecretKey", Env("MINIO_ROOT_PASSWORD") ?? "minioadmin")
    .WithEnvironment("Minio__UseSsl", "false")
    // 模型路由：DeepSeek 生成 + Qwen 向量化（短名 → Ai__ 配置键的映射）
    .WithEnvironment("Ai__DeepSeekApiKey", Env("DEEPSEEK_API_KEY") ?? string.Empty)
    .WithEnvironment("Ai__DeepSeekBaseUrl", Env("DEEPSEEK_BASE_URL") ?? "https://api.deepseek.com")
    .WithEnvironment("Ai__DeepSeekModel", Env("DEEPSEEK_MODEL") ?? "deepseek-chat")
    .WithEnvironment("Ai__QwenApiKey", Env("QWEN_API_KEY") ?? string.Empty)
    .WithEnvironment("Ai__QwenBaseUrl", Env("QWEN_BASE_URL") ?? "https://dashscope.aliyuncs.com/compatible-mode/v1")
    .WithEnvironment("Ai__QwenEmbeddingModel", Env("QWEN_EMBEDDING_MODEL") ?? "text-embedding-v3");

builder.Build().Run();
