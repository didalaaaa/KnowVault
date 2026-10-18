using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Testcontainers.Minio;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace KnowVault.IntegrationTests
{
	/// <summary>
	/// 四件套容器：PostgreSQL / RabbitMQ / MinIO / Qdrant
	/// </summary>
	public sealed class TestEnvironment : IAsyncLifetime
	{
		/// <summary>当前测试环境的连接信息，供 ABP 测试模块读取</summary>
		public static TestEnvironment Current { get; private set; } = null!;

		public string PostgresConnectionString { get; private set; } = string.Empty;

		public string RabbitMqConnectionString { get; private set; } = string.Empty;

		public string QdrantEndpoint { get; private set; } = string.Empty;

		public string MinioEndpoint { get; private set; } = string.Empty;

		public string MinioAccessKey { get; private set; } = string.Empty;

		public string MinioSecretKey { get; private set; } = string.Empty;

		/// <summary>MinIO 容器是否可用；本机镜像不可达时用内存存储兜底</summary>
		public static bool MinioAvailable { get; private set; }

		private PostgreSqlContainer? _postgres;
		private RabbitMqContainer? _rabbitMq;
		private MinioContainer? _minio;
		private IContainer? _qdrant;

		/// <inheritdoc />
		public async Task InitializeAsync()
		{
			_postgres = new PostgreSqlBuilder("postgres:16-alpine")
				.WithDatabase("knowvault_test")
				.WithUsername("postgres")
				.WithPassword("postgres")
				.Build();

			_rabbitMq = new RabbitMqBuilder("rabbitmq:3-management-alpine")
				.WithUsername("guest")
				.WithPassword("guest")
				.Build();

			_minio = new MinioBuilder("minio/minio:RELEASE.2025-04-22T22-12-26Z")
				.WithUsername("minioadmin")
				.WithPassword("minioadmin")
				.Build();

			_qdrant = new ContainerBuilder("qdrant/qdrant:v1.15.0")
				.WithPortBinding(6334, true)
				.WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(6334))
				.Build();

			await Task.WhenAll(
				_postgres.StartAsync(),
				_rabbitMq.StartAsync(),
				_qdrant.StartAsync());

			PostgresConnectionString = _postgres.GetConnectionString();
			RabbitMqConnectionString = _rabbitMq.GetConnectionString();
			QdrantEndpoint = $"http://127.0.0.1:{_qdrant.GetMappedPublicPort(6334)}";

			MinioAvailable = Environment.GetEnvironmentVariable("KNOWVAULT_SKIP_MINIO") != "1";
			if (MinioAvailable)
			{
				await _minio.StartAsync();
				MinioEndpoint = $"127.0.0.1:{_minio.GetMappedPublicPort(9000)}";
				MinioAccessKey = _minio.GetAccessKey();
				MinioSecretKey = _minio.GetSecretKey();
			}

			Current = this;
		}

		/// <inheritdoc />
		public async Task DisposeAsync()
		{
			if (_qdrant is not null) await _qdrant.DisposeAsync();
			if (_minio is not null && MinioAvailable) await _minio.DisposeAsync();
			if (_rabbitMq is not null) await _rabbitMq.DisposeAsync();
			if (_postgres is not null) await _postgres.DisposeAsync();
		}
	}
}
