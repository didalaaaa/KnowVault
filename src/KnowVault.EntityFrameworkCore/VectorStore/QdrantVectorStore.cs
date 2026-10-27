using KnowVault.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using Volo.Abp.DependencyInjection;
using static Qdrant.Client.Grpc.Conditions;

namespace KnowVault.EntityFrameworkCore
{
	/// <summary>
	/// Qdrant 实现的向量库（单例：内部持有 gRPC channel）。
	/// </summary>
	public class QdrantVectorStore : IVectorStore, ISingletonDependency
	{
		private readonly QdrantClient _client;
		private readonly QdrantOptions _options;
		private readonly ILogger<QdrantVectorStore> _logger;
		private readonly SemaphoreSlim _ensureLock = new(1, 1);
		private bool _collectionEnsured;

		/// <summary>构造函数。</summary>
		/// <param name="client">Qdrant 客户端（模块里注册为单例）。</param>
		/// <param name="options">Qdrant 配置。</param>
		/// <param name="logger">日志。</param>
		public QdrantVectorStore(QdrantClient client, IOptions<QdrantOptions> options, ILogger<QdrantVectorStore> logger)
		{
			_client = client;
			_options = options.Value;
			_logger = logger;
		}

		/// <inheritdoc />
		public async Task EnsureCollectionAsync(CancellationToken cancellationToken = default)
		{
			if (_collectionEnsured) return;

			await _ensureLock.WaitAsync(cancellationToken);
			try
			{
				if (_collectionEnsured) return;

				if (!await _client.CollectionExistsAsync(_options.CollectionName, cancellationToken))
				{
					await CreateCollectionAsync(cancellationToken);
					_logger.LogInformation("已创建 Qdrant collection {Collection}（维度 {Size}，Cosine + 稀疏 {Sparse}）", _options.CollectionName, _options.VectorSize, VectorSchema.SparseVectorName);
				}
				else
				{
					var info = await _client.GetCollectionInfoAsync(_options.CollectionName, cancellationToken);
					if (info.Config?.Params?.SparseVectorsConfig is not { } sparseVectors ||
						!sparseVectors.Map.ContainsKey(VectorSchema.SparseVectorName))
					{
						throw new VectorSchemaMismatchException(
							$"Qdrant collection {_options.CollectionName} 缺少稀疏向量 {VectorSchema.SparseVectorName}；请调用 POST /api/app/maintenance/rebuild-vector-index 重建索引并重新处理文档。");
					}
				}

				await EnsurePayloadIndexesAsync(cancellationToken);
				_collectionEnsured = true;
			}
			finally
			{
				_ensureLock.Release();
			}
		}

		/// <inheritdoc />
		public async Task UpsertAsync(IReadOnlyList<VectorPoint> points, CancellationToken cancellationToken = default)
		{
			if (points.Count == 0) return;

			await EnsureCollectionAsync(cancellationToken);

			var structs = points.Select(point => new PointStruct
			{
				Id = new PointId { Uuid = point.Id.ToString() },
				Vectors = BuildVectors(point),
				Payload = { point.Payload.ToDictionary(kv => kv.Key, kv => ToValue(kv.Value)) }
			}).ToList();

			await _client.UpsertAsync(_options.CollectionName, structs, cancellationToken: cancellationToken);
			_logger.LogInformation("已写入 Qdrant {Count} 个向量点", structs.Count);
		}

		/// <inheritdoc />
		public async Task<IReadOnlyList<VectorSearchHit>> SearchAsync(
			float[] queryVector,
			VectorFilter filter,
			int topK,
			CancellationToken cancellationToken = default)
		{
			if (filter.KnowledgeBaseIds.Count == 0) return [];

			await EnsureCollectionAsync(cancellationToken);

			// 用新版 Query API（1.19 起 SearchAsync 已标记过时）；显式开启 payload 回传
			var points = await _client.QueryAsync(
				_options.CollectionName,
				query: queryVector,
				filter: BuildFilter(filter),
				limit: (ulong)topK,
				payloadSelector: new WithPayloadSelector { Enable = true },
				cancellationToken: cancellationToken);

			return [.. points.Select(ToHit)];
		}

		/// <inheritdoc />
		public async Task<IReadOnlyList<VectorSearchHit>> SearchSparseAsync(
			SparseVectorData sparseVector,
			VectorFilter filter,
			int topK,
			CancellationToken cancellationToken = default)
		{
			if (sparseVector.IsEmpty || topK <= 0 || filter.KnowledgeBaseIds.Count == 0) return [];

			await EnsureCollectionAsync(cancellationToken);

			var points = await _client.QueryAsync(
				_options.CollectionName,
				query: new Query { Nearest = (sparseVector.Values, sparseVector.Indices) },
				usingVector: VectorSchema.SparseVectorName,
				filter: BuildFilter(filter),
				limit: (ulong)topK,
				payloadSelector: new WithPayloadSelector { Enable = true },
				cancellationToken: cancellationToken);

			return [.. points.Select(ToHit)];
		}

		/// <inheritdoc />
		public async Task RecreateCollectionAsync(CancellationToken cancellationToken = default)
		{
			await _ensureLock.WaitAsync(cancellationToken);
			try
			{
				if (await _client.CollectionExistsAsync(_options.CollectionName, cancellationToken))
				{
					await _client.DeleteCollectionAsync(_options.CollectionName, cancellationToken: cancellationToken);
				}

				await CreateCollectionAsync(cancellationToken);
				await EnsurePayloadIndexesAsync(cancellationToken);
				_collectionEnsured = true;
				_logger.LogWarning("已重建 Qdrant collection {Collection}（dense + sparse {Sparse}）", _options.CollectionName, VectorSchema.SparseVectorName);
			}
			finally
			{
				_ensureLock.Release();
			}
		}

		/// <inheritdoc />
		public async Task DeleteByDocumentAsync(
			Guid tenantId,
			Guid knowledgeBaseId,
			Guid documentId,
			CancellationToken cancellationToken = default)
		{
			await EnsureCollectionAsync(cancellationToken);

			var filter = new Filter
			{
				Must =
				{
					MatchKeyword(ChunkPayload.TenantId, tenantId.ToString()),
					MatchKeyword(ChunkPayload.KnowledgeBaseId, knowledgeBaseId.ToString()),
					MatchKeyword(ChunkPayload.DocumentId, documentId.ToString())
				}
			};

			await _client.DeleteAsync(_options.CollectionName, filter, wait: true, cancellationToken: cancellationToken);
			_logger.LogInformation("已删除文档 {DocumentId} 的 Qdrant 向量点", documentId);
		}

		private async Task CreateCollectionAsync(CancellationToken cancellationToken)
		{
			await _client.CreateCollectionAsync(
				_options.CollectionName,
				new VectorParams { Size = _options.VectorSize, Distance = Distance.Cosine },
				sparseVectorsConfig: new SparseVectorConfig
				{
					Map = { [VectorSchema.SparseVectorName] = new SparseVectorParams { Modifier = Modifier.Idf } }
				},
				cancellationToken: cancellationToken);
		}

		// 按 payload 过滤删除/检索必须建 keyword 索引，否则 Qdrant 会全量扫描甚至拒绝过滤
		private async Task EnsurePayloadIndexesAsync(CancellationToken cancellationToken)
		{
			foreach (var field in new[] { ChunkPayload.TenantId, ChunkPayload.KnowledgeBaseId, ChunkPayload.DocumentId })
			{
				await _client.CreatePayloadIndexAsync(
					_options.CollectionName,
					field,
					PayloadSchemaType.Keyword,
					cancellationToken: cancellationToken);
			}
		}

		// 单租户运行时 filter.TenantId 为 Guid.Empty，与写入侧归一化保持一致
		// 注意：GUID 以 keyword（字符串）落 payload，因此必须用 MatchKeyword 而不是数值型 Match
		private static Filter BuildFilter(VectorFilter filter)
		{
			var result = new Filter();
			result.Must.Add(MatchKeyword(ChunkPayload.TenantId, filter.TenantId.ToString()));

			if (filter.KnowledgeBaseIds.Count == 1)
			{
				result.Must.Add(MatchKeyword(ChunkPayload.KnowledgeBaseId, filter.KnowledgeBaseIds[0].ToString()));
			}
			else
			{
				result.Must.Add(Match(ChunkPayload.KnowledgeBaseId, [.. filter.KnowledgeBaseIds.Select(id => id.ToString())]));
			}

			return result;
		}

		private static Vectors BuildVectors(VectorPoint point)
		{
			var vectors = new Dictionary<string, Vector>
			{
				[string.Empty] = point.Vector
			};

			if (point.SparseVector is { IsEmpty: false } sparse)
			{
				vectors[VectorSchema.SparseVectorName] = (sparse.Values, sparse.Indices);
			}

			return vectors;
		}

		private static VectorSearchHit ToHit(ScoredPoint point) => new()
		{
			Id = Guid.TryParse(point.Id.Uuid, out var id) ? id : Guid.Empty,
			Score = point.Score,
			Payload = point.Payload.ToDictionary(kv => kv.Key, kv => FromValue(kv.Value))
		};

		private static Value ToValue(object value) => value switch
		{
			string text => new Value { StringValue = text },
			int number => new Value { IntegerValue = number },
			long number => new Value { IntegerValue = number },
			bool flag => new Value { BoolValue = flag },
			double number => new Value { DoubleValue = number },
			_ => new Value { StringValue = value.ToString() ?? string.Empty }
		};

		private static object FromValue(Value value) => value.KindCase switch
		{
			Value.KindOneofCase.StringValue => value.StringValue,
			Value.KindOneofCase.IntegerValue => value.IntegerValue,
			Value.KindOneofCase.DoubleValue => value.DoubleValue,
			Value.KindOneofCase.BoolValue => value.BoolValue,
			_ => string.Empty
		};
	}
}
