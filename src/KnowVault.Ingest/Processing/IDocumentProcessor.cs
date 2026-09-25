namespace KnowVault.Ingest
{

	/// <summary>
	/// 文档处理入口（P0 为占位）。P1 起改由 RabbitMQ/MassTransit 消费队列驱动；
	/// 处理链路：解析 -> 切片 -> Qwen 向量化 -> 写入 Qdrant，并把 Document 状态推进 Ready/Failed。
	/// </summary>
	public interface IDocumentProcessor
	{
		Task ProcessPendingDocumentsAsync(CancellationToken cancellationToken = default);
	}

}
