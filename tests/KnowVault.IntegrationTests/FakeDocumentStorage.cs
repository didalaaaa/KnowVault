using System.Collections.Concurrent;
using KnowVault.Domain;

namespace KnowVault.IntegrationTests
{
	/// <summary>
	/// 内存对象存储：本机无法拉取 MinIO 镜像时兜底，CI 仍使用真实 MinIO 容器
	/// </summary>
	public sealed class FakeDocumentStorage : IDocumentStorage
	{
		private readonly ConcurrentDictionary<string, byte[]> _objects = new();

		/// <inheritdoc />
		public Task EnsureBucketAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

		/// <inheritdoc />
		public async Task<string> PutAsync(string objectKey, Stream content, string contentType, CancellationToken cancellationToken = default)
		{
			using var buffer = new MemoryStream();
			await content.CopyToAsync(buffer, cancellationToken);
			_objects[objectKey] = buffer.ToArray();
			return objectKey;
		}

		/// <inheritdoc />
		public Task<Stream> GetAsync(string objectKey, CancellationToken cancellationToken = default)
		{
			if (!_objects.TryGetValue(objectKey, out var bytes)) throw new FileNotFoundException(objectKey);
			return Task.FromResult<Stream>(new MemoryStream(bytes, writable: false));
		}

		/// <inheritdoc />
		public Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default)
		{
			_objects.TryRemove(objectKey, out _);
			return Task.CompletedTask;
		}
	}
}
