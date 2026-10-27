using KnowVault.Domain;

namespace KnowVault.IntegrationTests
{
	/// <summary>
	/// 固定 1024 维向量，避免集成测试真实调用 SiliconFlow
	/// </summary>
	public sealed class FakeEmbeddingService : IEmbeddingService
	{
		/// <inheritdoc />
		public Task<EmbeddingResult> EmbedAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
		{
			return Task.FromResult(new EmbeddingResult
			{
				Vectors = [.. texts.Select(_ => Enumerable.Repeat(0.01f, 1024).ToArray())],
				Model = "fake-embedding",
				TotalTokens = texts.Sum(text => text.Length),
				IsEstimated = true
			});
		}
	}
}
