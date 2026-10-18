using KnowVault.Domain;

namespace KnowVault.IntegrationTests
{
	/// <summary>
	/// 记录发布请求，不实际连 RabbitMQ（消费由 DocumentProcessor 直接驱动）
	/// </summary>
	public sealed class FakeDocumentIngestPublisher : IDocumentIngestPublisher
	{
		/// <summary>已发布的文档 ID</summary>
		public List<Guid> Published { get; } = [];

		/// <inheritdoc />
		public Task PublishAsync(Guid documentId, Guid? tenantId, CancellationToken cancellationToken = default)
		{
			Published.Add(documentId);
			return Task.CompletedTask;
		}
	}
}
