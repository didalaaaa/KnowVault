using System.ComponentModel.DataAnnotations;

namespace KnowVault.Application.Contracts
{

	/// <summary>问答入参（P0 最小闭环的 API 契约）。</summary>
	public class AskQuestionInput
	{
		/// <summary>
		/// 可选：提问范围包含的知识库 ID；为空且未选「全部」时使用默认知识库
		/// </summary>
		public List<Guid>? KnowledgeBaseIds { get; set; }

		/// <summary>
		/// 是否对当前用户可访问的全部知识库提问；与 <see cref="KnowledgeBaseIds"/> 互斥
		/// </summary>
		public bool AllKnowledgeBases { get; set; }

		/// <summary>可选：已有会话；为空则新建会话。</summary>
		public Guid? ConversationId { get; set; }

		/// <summary>可选：覆盖检索条数（top-k）；为空或 0 时用服务端配置（上限 8）。</summary>
		public int? TopK { get; set; }

		/// <summary>重新生成：不插入新的用户消息，历史中剔除上一轮问答；必须带会话 ID。</summary>
		public bool Regenerate { get; set; }

		/// <summary>
		/// 可选：本次提问是否启用混合检索；为空时使用服务端配置
		/// </summary>
		public bool? HybridEnabled { get; set; }

		/// <summary>
		/// 可选：本次提问是否启用查询改写；为空时使用服务端配置
		/// </summary>
		public bool? RewriteEnabled { get; set; }

		[Required]
		[StringLength(2000)]
		public string Question { get; set; } = string.Empty;
	}

}
