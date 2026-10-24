using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowVault.Domain
{

	/// <summary>
	/// 知识库（多租户起步，结构预留 TenantId 隔离）。
	/// </summary>
	public class KnowledgeBase : CreationAuditedAggregateRoot<Guid>, IMultiTenant
	{
		public string Name { get; set; } = default!;

		public string? Description { get; set; }

		/// <summary>多租户隔离 ID；ABP 多租户是一等公民，先单租户运行。</summary>
		public Guid? TenantId { get; set; }

		/// <summary>切片长度（字符）；为空时使用全局配置。</summary>
		public int? ChunkSizeChars { get; private set; }

		/// <summary>切片重叠（字符）；为空时使用全局配置。</summary>
		public int? ChunkOverlapChars { get; private set; }

		/// <summary>检索条数；为空时使用全局配置。</summary>
		public int? TopK { get; private set; }

		/// <summary>自定义系统提示词；为空时使用内置模板。</summary>
		public string? SystemPrompt { get; private set; }

		protected KnowledgeBase()
		{
		}

		public KnowledgeBase(
			Guid id,
			string name,
			string? description = null,
			Guid? tenantId = null) : base(id)
		{
			Name = name;
			Description = description;
			TenantId = tenantId;
		}

		public void Update(string name, string? description = null)
		{
			Name = name;
			Description = description;
		}

		/// <summary>
		/// 更新切片与检索设置；传 null 表示回落全局配置
		/// </summary>
		/// <param name="chunkSizeChars">切片长度（字符）。</param>
		/// <param name="chunkOverlapChars">切片重叠（字符）。</param>
		/// <param name="topK">检索条数。</param>
		/// <param name="systemPrompt">自定义系统提示词。</param>
		public void UpdateSettings(int? chunkSizeChars, int? chunkOverlapChars, int? topK, string? systemPrompt)
		{
			ChunkSizeChars = chunkSizeChars;
			ChunkOverlapChars = chunkOverlapChars;
			TopK = topK;
			SystemPrompt = systemPrompt;
		}
	}

}
