using System.ComponentModel.DataAnnotations;
using KnowVault.Domain.Shared;

namespace KnowVault.Application.Contracts
{
	/// <summary>
	/// 设置或取消消息反馈的入参
	/// </summary>
	public class SetFeedbackInput
	{
		/// <summary>
		/// 助手消息 ID
		/// </summary>
		public Guid MessageId { get; set; }

		/// <summary>
		/// 评价类型；为空表示取消已有反馈
		/// </summary>
		public FeedbackRatingEnum? Rating { get; set; }

		/// <summary>
		/// 可选备注
		/// </summary>
		[StringLength(1000)]
		public string? Comment { get; set; }
	}
}
