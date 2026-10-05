namespace KnowVault.EntityFrameworkCore
{
	/// <summary>Qdrant 连接与 collection 参数。</summary>
	public class QdrantOptions
	{
		/// <summary>gRPC 端点（注意不是 6333 的 REST 端口，而是 6334）。</summary>
		public string Endpoint { get; set; } = "http://localhost:6334";

		/// <summary>向量维度，必须与 embedding 模型输出一致（text-embedding-v3 = 1024）。</summary>
		public ulong VectorSize { get; set; } = 1024;

		/// <summary>单一 collection 名（禁止按知识库拆 collection）。</summary>
		public string CollectionName { get; set; } = "knowvault_chunks";

		/// <summary>可选的 API Key（自托管默认无）。</summary>
		public string? ApiKey { get; set; }
	}
}
