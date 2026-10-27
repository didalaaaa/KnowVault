namespace KnowVault.Application.Contracts
{
	/// <summary>
	/// 向量索引重建结果
	/// </summary>
	public class RebuildVectorIndexResultDto
	{
		/// <summary>
		/// 已重新排队的文档数
		/// </summary>
		public int RequeuedCount { get; set; }

		/// <summary>
		/// 因仍在处理中而跳过的文档数
		/// </summary>
		public int SkippedCount { get; set; }

		/// <summary>
		/// 被跳过文档的 ID
		/// </summary>
		public IReadOnlyList<Guid> SkippedDocumentIds { get; set; } = [];
	}
}
