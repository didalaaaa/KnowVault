namespace KnowVault.Domain
{
	/// <summary>原始文档的对象存储抽象（P1 实现为 MinIO）。</summary>
	public interface IDocumentStorage
	{
		/// <summary>幂等创建 bucket。</summary>
		/// <param name="cancellationToken">取消令牌。</param>
		Task EnsureBucketAsync(CancellationToken cancellationToken = default);

		/// <summary>写入对象。</summary>
		/// <param name="objectKey">对象键。</param>
		/// <param name="content">内容流（须支持 Length）。</param>
		/// <param name="contentType">MIME 类型。</param>
		/// <param name="cancellationToken">取消令牌。</param>
		/// <returns>写入的对象键（回显，便于调用方落库）。</returns>
		Task<string> PutAsync(string objectKey, Stream content, string contentType, CancellationToken cancellationToken = default);

		/// <summary>读取对象。</summary>
		/// <param name="objectKey">对象键。</param>
		/// <param name="cancellationToken">取消令牌。</param>
		/// <returns>可读流（调用方负责释放）。</returns>
		Task<Stream> GetAsync(string objectKey, CancellationToken cancellationToken = default);
	}
}
