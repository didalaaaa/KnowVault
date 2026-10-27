using System.Runtime.CompilerServices;
using KnowVault.Ai;
using KnowVault.Domain;
using KnowVault.Domain.Shared;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace KnowVault.Tests
{
	/// <summary>
	/// RagService 混合检索测试：RRF 融合顺序、权重开关与降级路径
	/// </summary>
	public class RagServiceHybridTests
	{
		private static readonly Guid KnowledgeBaseId = Guid.Parse("00000000-0000-0000-0000-000000000001");

		/// <summary>
		/// dense 与 keyword 两路命中按 RRF 融合，跨分支命中的切片得分累加
		/// </summary>
		[Fact]
		public async Task Hybrid_fuses_dense_and_keyword_branches()
		{
			var store = new FakeVectorStore(
				[Hit("甲", 0.90, 0), Hit("乙", 0.80, 1)],
				[Hit("乙", 0.70, 1), Hit("丙", 0.60, 2)]);
			var service = CreateService(store);

			var result = await service.RetrieveAsync(new RagRequest { Question = "甲乙", TopK = 2 });

			Assert.Equal(new[] { "乙", "甲" }, result.Chunks.Select(chunk => chunk.DocumentTitle));
			Assert.Equal(1.0 / 61 + 1.0 / 62, result.Chunks[0].Score, 6);
			Assert.Equal(1.0 / 61, result.Chunks[1].Score, 6);
			Assert.Equal(new[] { "甲乙" }, result.RewriteQueries);
		}

		/// <summary>
		/// 关键词权重为 0 时只走 dense，不调用稀疏检索
		/// </summary>
		[Fact]
		public async Task Keyword_weight_zero_uses_dense_only()
		{
			var store = new FakeVectorStore([Hit("甲", 0.90, 0), Hit("乙", 0.80, 1)], [Hit("丙", 0.70, 2)]);
			var service = CreateService(store, new HybridOptions { KeywordWeight = 0 });

			var result = await service.RetrieveAsync(new RagRequest { Question = "甲乙", TopK = 2 });

			Assert.Equal(0, store.SparseCallCount);
			Assert.Equal(new[] { "甲", "乙" }, result.Chunks.Select(chunk => chunk.DocumentTitle));
		}

		/// <summary>
		/// dense 权重为 0 时只走关键词分支
		/// </summary>
		[Fact]
		public async Task Dense_weight_zero_uses_keyword_only()
		{
			var store = new FakeVectorStore([Hit("甲", 0.90, 0)], [Hit("丙", 0.70, 2), Hit("乙", 0.60, 1)]);
			var service = CreateService(store, new HybridOptions { DenseWeight = 0 });

			var result = await service.RetrieveAsync(new RagRequest { Question = "丙乙", TopK = 2 });

			Assert.Equal(0, store.DenseCallCount);
			Assert.Equal(new[] { "丙", "乙" }, result.Chunks.Select(chunk => chunk.DocumentTitle));
		}

		/// <summary>
		/// 稀疏检索抛错时退回纯 dense，问答仍然成功
		/// </summary>
		[Fact]
		public async Task Sparse_failure_falls_back_to_dense()
		{
			var store = new FakeVectorStore(
				[Hit("甲", 0.90, 0), Hit("乙", 0.80, 1)],
				[],
				sparseException: new AiCallException("稀疏检索炸了"));
			var service = CreateService(store);

			var answer = await service.AnswerAsync(new RagRequest { Question = "问题", TopK = 2 });

			Assert.Equal("答案", answer.Answer);
			Assert.Equal(new[] { "甲", "乙" }, answer.Citations.Select(citation => citation.Title));
			Assert.Equal(1, store.SparseCallCount);
		}

		/// <summary>
		/// 问题切不出词元时跳过稀疏分支，不影响 dense 召回
		/// </summary>
		[Fact]
		public async Task Sparse_query_without_tokens_is_skipped()
		{
			var store = new FakeVectorStore([Hit("甲", 0.90, 0)], []);
			var service = CreateService(store);

			var result = await service.RetrieveAsync(new RagRequest { Question = "的", TopK = 1 });

			Assert.Single(result.Chunks);
			Assert.Equal(0, store.SparseCallCount);
		}

		/// <summary>
		/// 混合路径按 PerQueryCandidateCount 限制每条查询的召回数
		/// </summary>
		[Fact]
		public async Task Hybrid_uses_per_query_candidate_count()
		{
			var store = new FakeVectorStore([Hit("甲", 0.90, 0)], [Hit("甲", 0.80, 0)]);
			var service = CreateService(store, rerankEnabled: true);

			await service.RetrieveAsync(new RagRequest { Question = "问题", TopK = 5 });

			Assert.Equal(10, store.LastDenseLimit);
			Assert.Equal(10, store.LastSparseLimit);
		}

		/// <summary>
		/// 关闭混合检索时保持 P4 快速路径：一次 dense 宽召回，不查稀疏
		/// </summary>
		[Fact]
		public async Task Fast_path_uses_rerank_candidate_count()
		{
			var store = new FakeVectorStore([Hit("甲", 0.90, 0)], [Hit("甲", 0.80, 0)]);
			var service = CreateService(store, hybridEnabled: false, rerankEnabled: true);

			await service.RetrieveAsync(new RagRequest { Question = "问题", TopK = 5 });

			Assert.Equal(20, store.LastDenseLimit);
			Assert.Equal(0, store.SparseCallCount);
		}

		/// <summary>
		/// 开启重排时引用得分替换为重排分
		/// </summary>
		[Fact]
		public async Task Rerank_replaces_fused_scores()
		{
			var store = new FakeVectorStore(
				[Hit("甲", 0.90, 0), Hit("乙", 0.80, 1)],
				[Hit("乙", 0.70, 1), Hit("丙", 0.60, 2)]);
			var rerank = new FakeRerankService(
				[new RerankResult { Index = 1, Score = 0.99 }, new RerankResult { Index = 0, Score = 0.11 }]);
			var service = CreateService(store, rerankEnabled: true, rerank: rerank);

			var result = await service.RetrieveAsync(new RagRequest { Question = "问题", TopK = 2 });

			Assert.Equal("甲", result.Chunks[0].DocumentTitle);
			Assert.Equal(0.99, result.Chunks[0].Score, 4);
			Assert.Equal("乙", result.Chunks[1].DocumentTitle);
			Assert.Equal(0.11, result.Chunks[1].Score, 4);
			Assert.Equal(1, rerank.CallCount);
		}

		/// <summary>
		/// dense 检索失败直接抛出，不做降级
		/// </summary>
		[Fact]
		public async Task Dense_failure_propagates()
		{
			var store = new FakeVectorStore([], [], denseException: new AiCallException("dense 炸了"));
			var service = CreateService(store);

			await Assert.ThrowsAsync<AiCallException>(() => service.RetrieveAsync(new RagRequest { Question = "问题", TopK = 2 }));
		}

		/// <summary>
		/// 构造被测服务（配置与日志用最简实现）
		/// </summary>
		/// <param name="store">假向量库</param>
		/// <param name="hybrid">混合检索参数</param>
		/// <param name="hybridEnabled">是否启用混合检索</param>
		/// <param name="rerankEnabled">是否启用重排</param>
		/// <param name="rerank">假重排服务</param>
		/// <returns>被测的 RagService</returns>
		private static RagService CreateService(
			FakeVectorStore store,
			HybridOptions? hybrid = null,
			bool hybridEnabled = true,
			bool rerankEnabled = false,
			FakeRerankService? rerank = null)
		{
			var options = Options.Create(new AiOptions
			{
				TopK = 5,
				RerankEnabled = rerankEnabled,
				RerankCandidateCount = 20,
				HybridEnabled = hybridEnabled,
				Hybrid = hybrid ?? new HybridOptions()
			});

			return new RagService(
				options,
				new FakeEmbeddingService(),
				store,
				new FakeChatService(),
				rerank ?? new FakeRerankService([]),
				NullLogger<RagService>.Instance);
		}

		/// <summary>
		/// 造一条带完整 payload 的检索命中，同一 chunkIndex 复用同一个点 ID
		/// </summary>
		/// <param name="title">文档标题</param>
		/// <param name="score">召回得分</param>
		/// <param name="chunkIndex">切片序号</param>
		/// <returns>检索命中</returns>
		private static VectorSearchHit Hit(string title, double score, int chunkIndex) => new()
		{
			Id = ChunkId(chunkIndex),
			Score = score,
			Payload = ChunkPayload.Create(Guid.Empty, KnowledgeBaseId, Guid.NewGuid(), chunkIndex, $"{title}的正文", title, string.Empty, DocumentSourceTypeEnum.Markdown)
		};

		private static Guid ChunkId(int chunkIndex) => Guid.Parse($"00000000-0000-0000-0000-00000000{chunkIndex:D4}");

		private sealed class FakeEmbeddingService : IEmbeddingService
		{
			public Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
				=> Task.FromResult<IReadOnlyList<float[]>>([.. texts.Select(_ => new float[8])]);
		}

		private sealed class FakeVectorStore : IVectorStore
		{
			private readonly IReadOnlyList<VectorSearchHit> _denseHits;
			private readonly IReadOnlyList<VectorSearchHit> _sparseHits;
			private readonly Exception? _denseException;
			private readonly Exception? _sparseException;

			public FakeVectorStore(
				IReadOnlyList<VectorSearchHit> denseHits,
				IReadOnlyList<VectorSearchHit> sparseHits,
				Exception? denseException = null,
				Exception? sparseException = null)
			{
				_denseHits = denseHits;
				_sparseHits = sparseHits;
				_denseException = denseException;
				_sparseException = sparseException;
			}

			public int DenseCallCount { get; private set; }

			public int SparseCallCount { get; private set; }

			public int LastDenseLimit { get; private set; }

			public int LastSparseLimit { get; private set; }

			public Task EnsureCollectionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

			public Task UpsertAsync(IReadOnlyList<VectorPoint> points, CancellationToken cancellationToken = default) => Task.CompletedTask;

			public Task<IReadOnlyList<VectorSearchHit>> SearchAsync(float[] queryVector, VectorFilter filter, int topK, CancellationToken cancellationToken = default)
			{
				DenseCallCount++;
				LastDenseLimit = topK;
				if (_denseException is not null) throw _denseException;
				return Task.FromResult<IReadOnlyList<VectorSearchHit>>([.. _denseHits.Take(topK)]);
			}

			public Task<IReadOnlyList<VectorSearchHit>> SearchSparseAsync(SparseVectorData sparseVector, VectorFilter filter, int topK, CancellationToken cancellationToken = default)
			{
				SparseCallCount++;
				LastSparseLimit = topK;
				if (_sparseException is not null) throw _sparseException;
				return Task.FromResult<IReadOnlyList<VectorSearchHit>>([.. _sparseHits.Take(topK)]);
			}

			public Task RecreateCollectionAsync(CancellationToken cancellationToken = default)
				=> Task.CompletedTask;

			public Task DeleteByDocumentAsync(Guid tenantId, Guid knowledgeBaseId, Guid documentId, CancellationToken cancellationToken = default)
				=> Task.CompletedTask;
		}

		private sealed class FakeChatService : ILlmChatService
		{
			public async IAsyncEnumerable<string> CompleteStreamAsync(
				string systemPrompt,
				IReadOnlyList<LlmChatMessage> messages,
				[EnumeratorCancellation] CancellationToken cancellationToken = default)
			{
				yield return "答";
				await Task.Yield();
				yield return "案";
			}
		}

		private sealed class FakeRerankService : IRerankService
		{
			private readonly IReadOnlyList<RerankResult> _results;

			public FakeRerankService(IReadOnlyList<RerankResult> results) => _results = results;

			public int CallCount { get; private set; }

			public Task<IReadOnlyList<RerankResult>> RerankAsync(string query, IReadOnlyList<string> documents, CancellationToken cancellationToken = default)
			{
				CallCount++;
				return Task.FromResult(_results);
			}
		}
	}
}
