using KnowVault.Domain;

namespace KnowVault.Eval
{
	/// <summary>
	/// 题号到金标切片、切片位置到切片 ID 的映射
	/// </summary>
	public class AnchorIndex
	{
		/// <summary>
		/// 每道题的金标切片 ID
		/// </summary>
		public IReadOnlyDictionary<string, IReadOnlyList<Guid>> GoldByQuestion { get; init; }
			= new Dictionary<string, IReadOnlyList<Guid>>();

		/// <summary>
		/// (文档 ID, 切片序号) 到切片 ID 的映射
		/// </summary>
		public IReadOnlyDictionary<(Guid DocumentId, int ChunkIndex), Guid> ChunkIdByPosition { get; init; }
			= new Dictionary<(Guid, int), Guid>();

		/// <summary>
		/// 知识库全部切片
		/// </summary>
		public IReadOnlyList<VectorSearchHit> Chunks { get; init; } = [];
	}

	/// <summary>
	/// 把金标里的原文锚点解析成实际切片 ID
	/// </summary>
	public static class AnchorResolver
	{
		/// <summary>
		/// 读取知识库全部切片并校验语料与锚点
		/// </summary>
		/// <param name="vectorStore">向量库</param>
		/// <param name="knowledgeBaseId">知识库 ID</param>
		/// <param name="tenantId">租户 ID</param>
		/// <param name="questions">金标题目</param>
		/// <param name="corpusDirectory">语料目录</param>
		/// <param name="cancellationToken">取消令牌</param>
		/// <returns>锚点映射</returns>
		/// <exception cref="InvalidOperationException">语料缺失或锚点无法解析</exception>
		public static async Task<AnchorIndex> ResolveAsync(
			IVectorStore vectorStore,
			Guid knowledgeBaseId,
			Guid tenantId,
			IReadOnlyList<GoldenQuestion> questions,
			string corpusDirectory,
			CancellationToken cancellationToken)
		{
			var chunks = await vectorStore.ScrollAsync(
				new VectorFilter { TenantId = tenantId, KnowledgeBaseIds = [knowledgeBaseId] },
				10000,
				cancellationToken);

			if (chunks.Count == 0)
			{
				throw new InvalidOperationException($"知识库 {knowledgeBaseId} 没有切片；请先上传语料并等待处理完成");
			}

			var byTitle = chunks.ToLookup(
				chunk => ChunkPayload.ReadString(chunk.Payload, ChunkPayload.DocumentTitle),
				StringComparer.OrdinalIgnoreCase);

			var expectedTitles = Directory
				.EnumerateFiles(corpusDirectory, "*.md", SearchOption.TopDirectoryOnly)
				.Select(Path.GetFileNameWithoutExtension)
				.Where(title => !string.IsNullOrWhiteSpace(title))
				.ToList();

			var missingDocuments = expectedTitles
				.Where(title => !byTitle.Contains(title!))
				.ToList();

			if (missingDocuments.Count > 0)
			{
				throw new InvalidOperationException($"知识库缺少语料文档：{string.Join("、", missingDocuments)}；请先上传 docs/eval/corpus 下的全部文件");
			}

			var errors = new List<string>();
			var goldByQuestion = new Dictionary<string, IReadOnlyList<Guid>>(StringComparer.OrdinalIgnoreCase);

			foreach (var question in questions)
			{
				var gold = new HashSet<Guid>();

				foreach (var relevant in question.Relevant)
				{
					var title = Path.GetFileNameWithoutExtension(relevant.Document);
					var matched = byTitle[title]
						.Where(chunk => ChunkPayload.ReadString(chunk.Payload, ChunkPayload.Content)
							.Contains(relevant.Anchor, StringComparison.Ordinal))
						.ToList();

					if (matched.Count == 0)
					{
						errors.Add($"{question.Id}：在《{relevant.Document}》找不到锚点「{relevant.Anchor}」");
						continue;
					}

					foreach (var chunk in matched) gold.Add(chunk.Id);
				}

				goldByQuestion[question.Id] = [.. gold];
			}

			if (errors.Count > 0)
			{
				throw new InvalidOperationException("锚点解析失败：\n - " + string.Join("\n - ", errors));
			}

			var positions = new Dictionary<(Guid, int), Guid>();
			foreach (var chunk in chunks)
			{
				var documentId = ChunkPayload.ReadGuid(chunk.Payload, ChunkPayload.DocumentId);
				var chunkIndex = ChunkPayload.ReadInt(chunk.Payload, ChunkPayload.ChunkIndex);
				positions[(documentId, chunkIndex)] = chunk.Id;
			}

			return new AnchorIndex
			{
				GoldByQuestion = goldByQuestion,
				ChunkIdByPosition = positions,
				Chunks = chunks
			};
		}
	}
}
