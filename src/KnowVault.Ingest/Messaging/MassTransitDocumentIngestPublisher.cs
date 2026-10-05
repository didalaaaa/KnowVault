using KnowVault.Domain;
using MassTransit;
using Volo.Abp.DependencyInjection;

namespace KnowVault.Ingest
{
	/// <summary>用 MassTransit 发布摄入请求。</summary>
	public class MassTransitDocumentIngestPublisher : IDocumentIngestPublisher, ITransientDependency
	{
		private readonly IPublishEndpoint _publishEndpoint;

		/// <summary>构造函数。</summary>
		/// <param name="publishEndpoint">MassTransit 发布端点。</param>
		public MassTransitDocumentIngestPublisher(IPublishEndpoint publishEndpoint)
		{
			_publishEndpoint = publishEndpoint;
		}

		/// <inheritdoc />
		public Task PublishAsync(Guid documentId, Guid? tenantId, CancellationToken cancellationToken = default)
			=> _publishEndpoint.Publish(
				new DocumentIngestRequested { DocumentId = documentId, TenantId = tenantId },
				cancellationToken);
	}
}
