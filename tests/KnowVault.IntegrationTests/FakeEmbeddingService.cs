using KnowVault.Domain;

namespace KnowVault.IntegrationTests
{
	/// <summary>
	/// 固定 1024 维向量，避免集成测试真实调用 Qwen
	/// </summary>
	public sealed class FakeEmbeddingService : IEmbeddingService
	{
		/// <inheritdoc />
		public Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
		{
			IReadOnlyList<float[]> vectors = texts
				.Select(_ => Enumerable.Repeat(0.01f, 1024).ToArray())
				.ToList();
			return Task.FromResult(vectors);
		}
	}
}
