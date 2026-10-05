using KnowVault.Domain.Shared;

namespace KnowVault.Domain
{
	/// <summary>
	/// Qdrant payload 的字段名与读写映射（单一 collection 的 payload 契约，见 TECH_STACK §9.1）。
	/// 字段名只在这里定义，避免 Ai 层读、Infra 层写时字符串漂移。
	/// </summary>
	public static class ChunkPayload
	{
		/// <summary>租户 ID（字符串形式的 GUID；null 归一化为 Guid.Empty）。</summary>
		public const string TenantId = "tenantId";

		/// <summary>知识库 ID。</summary>
		public const string KnowledgeBaseId = "knowledgeBaseId";

		/// <summary>文档 ID。</summary>
		public const string DocumentId = "documentId";

		/// <summary>切片序号（0 起）。</summary>
		public const string ChunkIndex = "chunkIndex";

		/// <summary>切片正文（检索命中后直接用于组装 Prompt，不回表）。</summary>
		public const string Content = "content";

		/// <summary>文档标题（冗余存一份，让引用溯源无需回表）。</summary>
		public const string DocumentTitle = "documentTitle";

		/// <summary>标题层级路径，例如「第三章 &gt; 3.2 部署」。</summary>
		public const string HeadingPath = "headingPath";

		/// <summary>来源格式（<see cref="DocumentSourceTypeEnum"/> 的 int 值）。</summary>
		public const string SourceType = "sourceType";

		/// <summary>构造 payload。</summary>
		/// <param name="tenantId">租户 ID（已归一化，无租户时为 <see cref="Guid.Empty"/>）。</param>
		/// <param name="knowledgeBaseId">知识库 ID。</param>
		/// <param name="documentId">文档 ID。</param>
		/// <param name="chunkIndex">切片序号。</param>
		/// <param name="content">切片正文。</param>
		/// <param name="documentTitle">文档标题。</param>
		/// <param name="headingPath">标题层级路径。</param>
		/// <param name="sourceType">来源格式。</param>
		/// <returns>可直接写入 Qdrant 的 payload 字典。</returns>
		public static Dictionary<string, object> Create(
			Guid tenantId,
			Guid knowledgeBaseId,
			Guid documentId,
			int chunkIndex,
			string content,
			string documentTitle,
			string headingPath,
			DocumentSourceTypeEnum sourceType) => new()
			{
				[TenantId] = tenantId.ToString(),
				[KnowledgeBaseId] = knowledgeBaseId.ToString(),
				[DocumentId] = documentId.ToString(),
				[ChunkIndex] = chunkIndex,
				[Content] = content,
				[DocumentTitle] = documentTitle,
				[HeadingPath] = headingPath,
				[SourceType] = (int)sourceType
			};

		/// <summary>读取 GUID 字段。</summary>
		/// <param name="payload">payload 字典。</param>
		/// <param name="key">字段名。</param>
		/// <returns>解析出的 GUID。</returns>
		/// <exception cref="InvalidOperationException">字段缺失或不是合法 GUID。</exception>
		public static Guid ReadGuid(IReadOnlyDictionary<string, object> payload, string key)
			=> payload.TryGetValue(key, out var value) && Guid.TryParse(Convert.ToString(value), out var guid)
				? guid
				: throw new InvalidOperationException($"payload 缺少合法的 {key} 字段。");

		/// <summary>读取字符串字段。</summary>
		/// <param name="payload">payload 字典。</param>
		/// <param name="key">字段名。</param>
		/// <returns>字符串值；缺失时返回空串。</returns>
		public static string ReadString(IReadOnlyDictionary<string, object> payload, string key)
			=> payload.TryGetValue(key, out var value) ? Convert.ToString(value) ?? string.Empty : string.Empty;

		/// <summary>读取整数字段。</summary>
		/// <param name="payload">payload 字典。</param>
		/// <param name="key">字段名。</param>
		/// <returns>整数值；缺失或无法转换时为 0。</returns>
		public static int ReadInt(IReadOnlyDictionary<string, object> payload, string key)
			=> payload.TryGetValue(key, out var value) && int.TryParse(Convert.ToString(value), out var number) ? number : 0;
	}
}
