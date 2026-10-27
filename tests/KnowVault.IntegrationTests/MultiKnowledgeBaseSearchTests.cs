using FluentAssertions;
using KnowVault.Domain;
using KnowVault.Domain.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace KnowVault.IntegrationTests
{
	/// <summary>
	/// 多库检索：同一 collection 内按 knowledgeBaseIds 过滤，dense 与 sparse 都要跨库命中
	/// </summary>
	[Collection(IntegrationCollection.Name)]
	public class MultiKnowledgeBaseSearchTests : KnowVaultIntegrationTestBase
	{
		/// <inheritdoc />
		public MultiKnowledgeBaseSearchTests(TestEnvironment environment)
		{
			_ = environment;
		}

		/// <summary>
		/// 指定两个知识库时两库切片都能命中；只指定一个库时另一个库的切片被过滤
		/// </summary>
		[Fact]
		public async Task Dense_and_sparse_search_should_span_multiple_knowledge_bases()
		{
			var vectorStore = GetRequiredService<IVectorStore>();
			await vectorStore.EnsureCollectionAsync();

			var knowledgeBaseA = Guid.NewGuid();
			var knowledgeBaseB = Guid.NewGuid();
			var documentA = Guid.NewGuid();
			var documentB = Guid.NewGuid();
			var textA = "多库检索知识库甲命中标记 KV-MULTI";
			var textB = "多库检索知识库乙命中标记 KV-MULTI";

			await vectorStore.UpsertAsync([
				CreatePoint(knowledgeBaseA, documentA, textA),
				CreatePoint(knowledgeBaseB, documentB, textB)
			]);

			try
			{
				var queryVector = Enumerable.Repeat(0.01f, 1024).ToArray();

				var bothDense = await vectorStore.SearchAsync(queryVector, Filter(knowledgeBaseA, knowledgeBaseB), 10);
				ReadDocumentIds(bothDense).Should().Contain([documentA, documentB]);

				var onlyADense = await vectorStore.SearchAsync(queryVector, Filter(knowledgeBaseA), 10);
				ReadDocumentIds(onlyADense).Should().ContainSingle(id => id == documentA);
				ReadDocumentIds(onlyADense).Should().NotContain(documentB);

				var sparseQuery = SparseVectorBuilder.BuildQuery("KV-MULTI");
				sparseQuery.Should().NotBeNull();

				var bothSparse = await vectorStore.SearchSparseAsync(sparseQuery!, Filter(knowledgeBaseA, knowledgeBaseB), 10);
				ReadDocumentIds(bothSparse).Should().Contain([documentA, documentB]);

				var onlyASparse = await vectorStore.SearchSparseAsync(sparseQuery!, Filter(knowledgeBaseA), 10);
				ReadDocumentIds(onlyASparse).Should().ContainSingle(id => id == documentA);
				ReadDocumentIds(onlyASparse).Should().NotContain(documentB);
			}
			finally
			{
				await vectorStore.DeleteByDocumentAsync(Guid.Empty, knowledgeBaseA, documentA);
				await vectorStore.DeleteByDocumentAsync(Guid.Empty, knowledgeBaseB, documentB);
			}
		}

		private static VectorFilter Filter(params Guid[] knowledgeBaseIds) => new()
		{
			TenantId = Guid.Empty,
			KnowledgeBaseIds = knowledgeBaseIds
		};

		private static VectorPoint CreatePoint(Guid knowledgeBaseId, Guid documentId, string text) => new()
		{
			Id = Guid.NewGuid(),
			Vector = Enumerable.Repeat(0.01f, 1024).ToArray(),
			SparseVector = SparseVectorBuilder.BuildDocument(text),
			Payload = ChunkPayload.Create(
				Guid.Empty,
				knowledgeBaseId,
				documentId,
				0,
				text,
				$"多库测试 {documentId:N}",
				string.Empty,
				DocumentSourceTypeEnum.Markdown)
		};

		private static IEnumerable<Guid> ReadDocumentIds(IReadOnlyList<VectorSearchHit> hits)
			=> hits.Select(hit => ChunkPayload.ReadGuid(hit.Payload, ChunkPayload.DocumentId));
	}
}
