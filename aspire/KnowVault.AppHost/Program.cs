var builder = DistributedApplication.CreateBuilder(args);

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
    .WithImage("qdrant/qdrant");

// RabbitMQ：文档解析消费队列（P1 接入 MassTransit）
var rabbitmq = builder.AddRabbitMQ("rabbitmq")
    .WithImage("rabbitmq:3-management");

// Seq：日志可视化
var seq = builder.AddSeq("seq");

// MinIO：S3 兼容对象存储，存原始文档。（无官方 Aspire 集成，用通用容器建模）
var minio = builder.AddContainer("minio", "minio/minio", "latest")
    .WithEnvironment("MINIO_ROOT_USER", "minioadmin")
    .WithEnvironment("MINIO_ROOT_PASSWORD", "minioadmin")
    .WithEndpoint(9000, 9000, "http")   // S3 API
    .WithEndpoint(9001, 9001, "http");  // 控制台 UI

// ---------- 应用 ----------
var api = builder.AddProject("knowvault-api", "../../src/KnowVault.HttpApi.Host/KnowVault.HttpApi.Host.csproj")
    .WithReference(database, "ConnectionStrings:Default")
    .WithReference(redis)
    .WithEnvironment("App:CorsOrigins", "http://localhost:5173");

builder.Build().Run();
