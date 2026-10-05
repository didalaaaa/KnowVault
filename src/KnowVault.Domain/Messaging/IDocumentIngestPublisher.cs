namespace KnowVault.Domain
{
	/// <summary>发布「文档待摄入」消息的抽象（实现放 Ingest 层，避免应用层耦合具体 MQ 框架）。</summary>
	public interface IDocumentIngestPublisher
	{
		/// <summary>发布摄入请求。</summary>
		/// <param name="documentId">文档 ID。</param>
		/// <param name="tenantId">租户 ID（可为 null，消费侧会归一化）。</param>
		/// <param name="cancellationToken">取消令牌。</param>
		Task PublishAsync(Guid documentId, Guid? tenantId, CancellationToken cancellationToken = default);
	}
}
