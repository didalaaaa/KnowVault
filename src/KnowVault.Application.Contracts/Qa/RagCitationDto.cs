namespace KnowVault.Application.Contracts
{

	/// <summary>引用溯源 DTO。</summary>
	public class RagCitationDto
	{
		public Guid DocumentId { get; set; }
		public string Title { get; set; } = string.Empty;
		public int ChunkIndex { get; set; }
		public double Score { get; set; }
	}

}
