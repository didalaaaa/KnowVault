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

		[Required]
		[StringLength(2000)]
		public string Question { get; set; } = string.Empty;
	}

}
