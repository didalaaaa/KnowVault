using System.ComponentModel.DataAnnotations;

namespace KnowVault.Application.Contracts
{

	/// <summary>问答入参（P0 最小闭环的 API 契约）。</summary>
	public class AskQuestionInput
	{
		/// <summary>可选：指定知识库；为空则使用默认/任意知识库。</summary>
		public Guid? KnowledgeBaseId { get; set; }

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
