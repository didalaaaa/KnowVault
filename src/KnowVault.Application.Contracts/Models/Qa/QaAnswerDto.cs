namespace KnowVault.Application.Contracts
{

	/// <summary>问答结果 DTO（答案 + 引用溯源）。</summary>
	public class QaAnswerDto
	{
		public Guid ConversationId { get; set; }
		public string Answer { get; set; } = string.Empty;
		public IReadOnlyList<RagCitationDto> Citations { get; set; } = Array.Empty<RagCitationDto>();
	}

}
