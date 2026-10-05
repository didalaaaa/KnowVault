using KnowVault.Domain;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KnowVault.Ingest
{
	/// <summary>
	/// 启动时幂等确保 Qdrant collection 存在。
	/// 失败只记日志不阻断启动（Qdrant 未就绪时，检索/写入会各自重试）。
	/// </summary>
	public class QdrantCollectionInitializer : IHostedService
	{
		private readonly IVectorStore _vectorStore;
		private readonly ILogger<QdrantCollectionInitializer> _logger;

		/// <summary>构造函数。</summary>
		/// <param name="vectorStore">向量库。</param>
		/// <param name="logger">日志。</param>
		public QdrantCollectionInitializer(IVectorStore vectorStore, ILogger<QdrantCollectionInitializer> logger)
		{
			_vectorStore = vectorStore;
			_logger = logger;
		}

		/// <inheritdoc />
		public async Task StartAsync(CancellationToken cancellationToken)
		{
			try
			{
				await _vectorStore.EnsureCollectionAsync(cancellationToken);
			}
			catch (Exception exception)
			{
				_logger.LogError(exception, "初始化 Qdrant collection 失败，应用继续启动");
			}
		}

		/// <inheritdoc />
		public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
	}
}
