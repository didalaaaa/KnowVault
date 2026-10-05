using KnowVault.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using Volo.Abp.DependencyInjection;

namespace KnowVault.EntityFrameworkCore
{
	/// <summary>MinIO 实现的对象存储。</summary>
	public class MinioDocumentStorage : IDocumentStorage, ISingletonDependency
	{
		private readonly IMinioClient _client;
		private readonly MinioOptions _options;
		private readonly ILogger<MinioDocumentStorage> _logger;
		private readonly SemaphoreSlim _bucketLock = new(1, 1);
		private bool _bucketEnsured;

		/// <summary>构造函数。</summary>
		/// <param name="client">MinIO 客户端（模块里注册为单例）。</param>
		/// <param name="options">MinIO 配置。</param>
		/// <param name="logger">日志。</param>
		public MinioDocumentStorage(IMinioClient client, IOptions<MinioOptions> options, ILogger<MinioDocumentStorage> logger)
		{
			_client = client;
			_options = options.Value;
			_logger = logger;
		}

		/// <inheritdoc />
		public async Task EnsureBucketAsync(CancellationToken cancellationToken = default)
		{
			if (_bucketEnsured)
			{
				return;
			}

			await _bucketLock.WaitAsync(cancellationToken);
			try
			{
				if (_bucketEnsured)
				{
					return;
				}

				var exists = await _client.BucketExistsAsync(
					new BucketExistsArgs().WithBucket(_options.BucketName), cancellationToken);
				if (!exists)
				{
					await _client.MakeBucketAsync(
						new MakeBucketArgs().WithBucket(_options.BucketName), cancellationToken);
					_logger.LogInformation("已创建 MinIO bucket {Bucket}", _options.BucketName);
				}

				_bucketEnsured = true;
			}
			finally
			{
				_bucketLock.Release();
			}
		}

		/// <inheritdoc />
		public async Task<string> PutAsync(
			string objectKey,
			Stream content,
			string contentType,
			CancellationToken cancellationToken = default)
		{
			await EnsureBucketAsync(cancellationToken);

			await _client.PutObjectAsync(new PutObjectArgs()
				.WithBucket(_options.BucketName)
				.WithObject(objectKey)
				.WithStreamData(content)
				.WithObjectSize(content.Length)
				.WithContentType(contentType), cancellationToken);

			return objectKey;
		}

		/// <inheritdoc />
		public async Task<Stream> GetAsync(string objectKey, CancellationToken cancellationToken = default)
		{
			// 先落到内存再返回：解析器需要可随机访问的流，且要避免对象存储连接被提前释放
			var memory = new MemoryStream();
			await _client.GetObjectAsync(new GetObjectArgs()
				.WithBucket(_options.BucketName)
				.WithObject(objectKey)
				.WithCallbackStream(stream => stream.CopyTo(memory)), cancellationToken);
			memory.Position = 0;
			return memory;
		}
	}
}
