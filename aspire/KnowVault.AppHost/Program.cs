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
    .WithEndpoint(9000, 9000, "http")   // S3 API
    .WithEndpoint(9001, 9001, "http");  // 控制台 UI

// ---------- 应用 ----------

// 迁移 + 种子（含默认知识库）：作为编排的一部分先跑完，避免 API 起来时表还不存在。
// 它自己 appsettings 里的连接串会被下面 WithReference 注入的值覆盖。
var dbMigrator = builder.AddProject("knowvault-dbmigrator", "../../src/KnowVault.DbMigrator/KnowVault.DbMigrator.csproj")
    .WithReference(database, "ConnectionStrings:Default")
    .WithReference(redis);

var api = builder.AddProject("knowvault-api", "../../src/KnowVault.HttpApi.Host/KnowVault.HttpApi.Host.csproj")
    .WaitForCompletion(dbMigrator)
    // 端口必须与 appsettings 的 App:SelfUrl / AuthServer:Authority 一致，
    // 否则 OpenIddict 的 issuer 与前端拿到的 authority 不一致，令牌校验必然失败
    .WithHttpsEndpoint(port: 44300, name: "https")
    .WithReference(database, "ConnectionStrings:Default")
    .WithReference(redis)
    .WithReference(rabbitmq)          // 注入 ConnectionStrings:rabbitmq（amqp://...）
    .WithEnvironment("App:CorsOrigins", "http://localhost:5173")
    // QdrantVectorStore 走 gRPC，必须注入 6334 的 grpc 端点（不是 6333 的 REST 端点）
    .WithEnvironment("Qdrant__Endpoint", qdrant.GetEndpoint("grpc"))
    // API 是宿主机进程、MinIO 是容器：用发布到宿主机的端口，因此是 localhost 而不是服务名
    .WithEnvironment("Minio__Endpoint", "localhost:9000")
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
