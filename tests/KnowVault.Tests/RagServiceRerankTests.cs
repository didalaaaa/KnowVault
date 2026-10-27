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
	/// RagService 接入重排的行为测试（用假实现替代向量库与模型调用）
	/// </summary>
	public class RagServiceRerankTests
	{
		private static readonly Guid KnowledgeBaseId = Guid.Parse("00000000-0000-0000-0000-000000000001");

		/// <summary>
		/// 重排返回的下标决定引用顺序，重排分替换向量分
		/// </summary>
		[Fact]
		public async Task Rerank_reorders_citations_and_replaces_scores()
		{
			var store = new FakeVectorStore([Hit("甲", 0.90, 0), Hit("乙", 0.80, 1)]);
			var rerank = new FakeRerankService([new RerankResult { Index = 1, Score = 0.95 }, new RerankResult { Index = 0, Score = 0.10 }]);
			var service = CreateService(store, rerank);

			var answer = await service.AnswerAsync(new RagRequest { Question = "问题", TopK = 2 });

			Assert.Equal("乙", answer.Citations[0].Title);
			Assert.Equal(0.95, answer.Citations[0].Score, 4);
			Assert.Equal(1, answer.Citations[0].ChunkIndex);
			Assert.Equal("甲", answer.Citations[1].Title);
			Assert.Equal(0.10, answer.Citations[1].Score, 4);
		}

		/// <summary>
		/// 一次性回答要把增量流拼成整段文本
		/// </summary>
		[Fact]
		public async Task AnswerAsync_joins_stream_deltas()
		{
			var store = new FakeVectorStore([Hit("甲", 0.90, 0)]);
			var service = CreateService(store, new FakeRerankService([]));

			var answer = await service.AnswerAsync(new RagRequest { Question = "问题", TopK = 1 });

			Assert.Equal("答案", answer.Answer);
		}

		/// <summary>
		/// 重排调用失败时要退回向量分顺序，且不能把异常抛给调用方
		/// </summary>
		[Fact]
		public async Task Rerank_failure_falls_back_to_vector_order()
		{
			var store = new FakeVectorStore([Hit("甲", 0.90, 0), Hit("乙", 0.80, 1)]);
			var rerank = new FakeRerankService(new AiCallException("重排炸了"));
			var service = CreateService(store, rerank);

			var answer = await service.AnswerAsync(new RagRequest { Question = "问题", TopK = 2 });

			Assert.Equal("甲", answer.Citations[0].Title);
			Assert.Equal(0.90, answer.Citations[0].Score, 4);
			Assert.Equal("乙", answer.Citations[1].Title);
		}

		/// <summary>
		/// 关闭重排时既不多召回，也不调用重排服务
		/// </summary>
		[Fact]
		public async Task Rerank_disabled_skips_rerank_service()
		{
			var store = new FakeVectorStore([Hit("甲", 0.90, 0), Hit("乙", 0.80, 1)]);
			var rerank = new FakeRerankService([new RerankResult { Index = 1, Score = 0.95 }]);
			var service = CreateService(store, rerank, rerankEnabled: false);

			var answer = await service.AnswerAsync(new RagRequest { Question = "问题", TopK = 2 });

			Assert.Equal(0, rerank.CallCount);
			Assert.Equal(2, store.LastLimit);
			Assert.Equal("甲", answer.Citations[0].Title);
		}

		/// <summary>
		/// 开启重排时要按 RerankCandidateCount 宽召回
		/// </summary>
		[Fact]
		public async Task Rerank_enabled_uses_candidate_count_for_recall()
		{
			var store = new FakeVectorStore([Hit("甲", 0.90, 0), Hit("乙", 0.80, 1)]);
			var rerank = new FakeRerankService([new RerankResult { Index = 0, Score = 0.5 }, new RerankResult { Index = 1, Score = 0.4 }]);
			var service = CreateService(store, rerank);

			await service.AnswerAsync(new RagRequest { Question = "问题", TopK = 2 });

			Assert.Equal(1, rerank.CallCount);
			Assert.Equal(20, store.LastLimit);
			Assert.Equal(2, rerank.LastCandidateCount);
		}

		/// <summary>
		/// 重排只负责打分：返回条数多于 topK 时由本地裁剪，顺序完全照重排返回的顺序
		/// </summary>
		[Fact]
		public async Task Rerank_returns_more_than_topK_is_trimmed_locally()
		{
			var store = new FakeVectorStore([Hit("甲", 0.90, 0), Hit("乙", 0.80, 1), Hit("丙", 0.70, 2)]);
			var rerank = new FakeRerankService([new RerankResult { Index = 2, Score = 0.99 }, new RerankResult { Index = 1, Score = 0.50 }, new RerankResult { Index = 0, Score = 0.10 }]);
			var service = CreateService(store, rerank);

			var answer = await service.AnswerAsync(new RagRequest { Question = "问题", TopK = 2 });

			Assert.Equal(2, answer.Citations.Count);
			Assert.Equal("丙", answer.Citations[0].Title);
			Assert.Equal(0.99, answer.Citations[0].Score, 4);
			Assert.Equal("乙", answer.Citations[1].Title);
			Assert.Equal(0.50, answer.Citations[1].Score, 4);
		}

		/// <summary>
		/// 构造被测服务（配置与日志用最简实现）
		/// </summary>
		/// <param name="store">假向量库</param>
		/// <param name="rerank">假重排服务</param>
		/// <param name="rerankEnabled">是否启用重排</param>
		/// <returns>被测的 RagService</returns>
		private static RagService CreateService(FakeVectorStore store, FakeRerankService rerank, bool rerankEnabled = true)
		{
			var options = Options.Create(new AiOptions
			{
				TopK = 5,
				RerankEnabled = rerankEnabled,
				RerankCandidateCount = 20,
				// 这组测试锁定 P4 纯向量路径，混合检索由 RagServiceHybridTests 覆盖
				HybridEnabled = false
			});

			return new RagService(options, new FakeEmbeddingService(), store, new FakeChatService(), rerank, NullLogger<RagService>.Instance);
		}

		/// <summary>
		/// 造一条带完整 payload 的检索命中
		/// </summary>
		/// <param name="title">文档标题（引用里展示的标题）</param>
		/// <param name="score">向量相似度分</param>
		/// <param name="chunkIndex">切片序号</param>
		/// <returns>检索命中</returns>
		private static VectorSearchHit Hit(string title, double score, int chunkIndex) => new()
		{
			Id = Guid.NewGuid(),
			Score = score,
			Payload = ChunkPayload.Create(Guid.Empty, KnowledgeBaseId, Guid.NewGuid(), chunkIndex, $"{title}的正文", title, string.Empty, DocumentSourceTypeEnum.Markdown)
		};

		private sealed class FakeEmbeddingService : IEmbeddingService
		{
			public Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
				=> Task.FromResult<IReadOnlyList<float[]>>([new float[8]]);
		}

		private sealed class FakeVectorStore : IVectorStore
		{
			private readonly IReadOnlyList<VectorSearchHit> _hits;

			public FakeVectorStore(IReadOnlyList<VectorSearchHit> hits) => _hits = hits;

			/// <summary>最后一次检索请求的条数，用于断言宽召回</summary>
			public int LastLimit { get; private set; }

			public Task EnsureCollectionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

			public Task UpsertAsync(IReadOnlyList<VectorPoint> points, CancellationToken cancellationToken = default) => Task.CompletedTask;

			public Task<IReadOnlyList<VectorSearchHit>> SearchAsync(float[] queryVector, VectorFilter filter, int topK, CancellationToken cancellationToken = default)
			{
				LastLimit = topK;
				return Task.FromResult<IReadOnlyList<VectorSearchHit>>(_hits.Take(topK).ToList());
			}

			public Task<IReadOnlyList<VectorSearchHit>> SearchSparseAsync(SparseVectorData sparseVector, VectorFilter filter, int topK, CancellationToken cancellationToken = default)
				=> Task.FromResult<IReadOnlyList<VectorSearchHit>>([]);

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
			private readonly IReadOnlyList<RerankResult>? _results;
			private readonly Exception? _exception;

			public FakeRerankService(IReadOnlyList<RerankResult> results) => _results = results;

			public FakeRerankService(Exception exception) => _exception = exception;

			/// <summary>调用次数，用于断言开关是否生效</summary>
			public int CallCount { get; private set; }

			/// <summary>最后一次请求的候选条数，用于断言候选全量交给重排打分</summary>
			public int LastCandidateCount { get; private set; }

			public Task<IReadOnlyList<RerankResult>> RerankAsync(string query, IReadOnlyList<string> documents, CancellationToken cancellationToken = default)
			{
				CallCount++;
				LastCandidateCount = documents.Count;
				if (_exception is not null) throw _exception;
				return Task.FromResult(_results!);
			}
		}
	}
}
