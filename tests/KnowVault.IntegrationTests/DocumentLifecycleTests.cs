using FluentAssertions;
using KnowVault.Application.Contracts;
using KnowVault.Domain;
using KnowVault.Domain.Shared;
using KnowVault.Ingest;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.Content;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace KnowVault.IntegrationTests
{
	/// <summary>
	/// 文档上传 → 解析 → 向量化 → 可检索 → 删除清理 的端到端测试
	/// </summary>
	public class DocumentLifecycleTests : KnowVaultIntegrationTestBase, IClassFixture<TestEnvironment>
	{
		/// <inheritdoc />
		public DocumentLifecycleTests(TestEnvironment environment)
		{
			_ = environment;
		}

		/// <summary>
		/// 上传 Markdown 后直接驱动处理器，应变为 Ready 且可被检索；删除后向量与切片都应清空
		/// </summary>
		[Fact]
		public async Task Upload_process_and_delete_should_keep_vector_and_chunk_store_consistent()
		{
			var documentAppService = GetRequiredService<IDocumentAppService>();
			var processor = GetRequiredService<IDocumentProcessor>();
			var vectorStore = GetRequiredService<IVectorStore>();
			var chunkRepository = GetRequiredService<IRepository<DocumentChunk, Guid>>();
			await vectorStore.EnsureCollectionAsync();

			var content = "# 集成测试\n\nKnowVault 的 P3 集成测试会真实写入 PostgreSQL、MinIO 与 Qdrant，关键词标记为 KV-2026。\n\n## 删除\n\n删除文档时必须同步清理切片与向量。";
			var upload = new UploadDocumentInput
			{
				KnowledgeBaseId = KnowVaultConsts.DefaultKnowledgeBaseId,
				Title = "P3 集成测试",
				File = new RemoteStreamContent(
					new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content)),
					"p3-integration.md",
					"text/markdown")
			};

			var document = await documentAppService.UploadAsync(upload, default);
			await processor.ProcessAsync(document.Id);

			var ready = await documentAppService.GetAsync(document.Id, default);
			ready.Status.Should().Be(DocumentStatusEnum.Ready);
			ready.IngestAttemptCount.Should().Be(1);

			var chunks = await chunkRepository.GetListAsync(x => x.DocumentId == document.Id);
			chunks.Should().NotBeEmpty();

			var hits = await vectorStore.SearchAsync(
				Enumerable.Repeat(0.01f, 1024).ToArray(),
				new VectorFilter
				{
					TenantId = Guid.Empty,
					KnowledgeBaseIds = [KnowVaultConsts.DefaultKnowledgeBaseId]
				},
				10);
			hits.Should().Contain(x => ChunkPayload.ReadGuid(x.Payload, ChunkPayload.DocumentId) == document.Id);

			var sparseQuery = SparseVectorBuilder.BuildQuery("KV-2026");
			sparseQuery.Should().NotBeNull();

			var sparseHits = await vectorStore.SearchSparseAsync(
				sparseQuery!,
				new VectorFilter
				{
					TenantId = Guid.Empty,
					KnowledgeBaseIds = [KnowVaultConsts.DefaultKnowledgeBaseId]
				},
				10);
			sparseHits.Should().Contain(x => ChunkPayload.ReadGuid(x.Payload, ChunkPayload.DocumentId) == document.Id);

			await documentAppService.DeleteAsync(document.Id, default);

			var afterDelete = async () => await documentAppService.GetAsync(document.Id, default);
			await afterDelete.Should().ThrowAsync<EntityNotFoundException>();

			var remainingChunks = await chunkRepository.GetListAsync(x => x.DocumentId == document.Id);
			remainingChunks.Should().BeEmpty();

			var remainingHits = await vectorStore.SearchAsync(
				Enumerable.Repeat(0.01f, 1024).ToArray(),
				new VectorFilter
				{
					TenantId = Guid.Empty,
					KnowledgeBaseIds = [KnowVaultConsts.DefaultKnowledgeBaseId]
				},
				10);
			remainingHits.Should().NotContain(x => ChunkPayload.ReadGuid(x.Payload, ChunkPayload.DocumentId) == document.Id);

			var remainingSparseHits = await vectorStore.SearchSparseAsync(
				sparseQuery!,
				new VectorFilter
				{
					TenantId = Guid.Empty,
					KnowledgeBaseIds = [KnowVaultConsts.DefaultKnowledgeBaseId]
				},
				10);
			remainingSparseHits.Should().NotContain(x => ChunkPayload.ReadGuid(x.Payload, ChunkPayload.DocumentId) == document.Id);
		}

		/// <summary>
		/// 重新处理前先清旧切片/向量，处理完不应出现重复切片
		/// </summary>
		[Fact]
		public async Task Reprocess_should_not_duplicate_chunks()
		{
			var documentAppService = GetRequiredService<IDocumentAppService>();
			var processor = GetRequiredService<IDocumentProcessor>();
			var chunkRepository = GetRequiredService<IRepository<DocumentChunk, Guid>>();

			var content = "# 重处理\n\n第一次处理与第二次处理产生的切片数量必须一致。";
			var document = await documentAppService.UploadAsync(new UploadDocumentInput
			{
				KnowledgeBaseId = KnowVaultConsts.DefaultKnowledgeBaseId,
				Title = "P3 重处理测试",
				File = new RemoteStreamContent(
					new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content)),
					"p3-reprocess.md",
					"text/markdown")
			}, default);

			await processor.ProcessAsync(document.Id);
			var firstCount = (await chunkRepository.GetListAsync(x => x.DocumentId == document.Id)).Count;
			firstCount.Should().BeGreaterThan(0);

			await documentAppService.ReprocessAsync(document.Id, default);
			await processor.ProcessAsync(document.Id);

			var secondCount = (await chunkRepository.GetListAsync(x => x.DocumentId == document.Id)).Count;
			secondCount.Should().Be(firstCount);

			await documentAppService.DeleteAsync(document.Id, default);
		}

		/// <summary>
		/// 同一知识库内同名文件（忽略大小写）重复上传应被拒绝，且不会产生第二条文档
		/// </summary>
		[Fact]
		public async Task Upload_with_duplicate_file_name_should_be_rejected()
		{
			var documentAppService = GetRequiredService<IDocumentAppService>();
			var documentRepository = GetRequiredService<IRepository<Document, Guid>>();

			var fileName = $"duplicate-{Guid.NewGuid():N}.md";
			var first = await documentAppService.UploadAsync(new UploadDocumentInput
			{
				KnowledgeBaseId = KnowVaultConsts.DefaultKnowledgeBaseId,
				Title = "去重测试",
				File = new RemoteStreamContent(
					new MemoryStream(System.Text.Encoding.UTF8.GetBytes("第一篇内容")),
					fileName,
					"text/markdown")
			}, default);

			try
			{
				var duplicateUpload = async () => await documentAppService.UploadAsync(new UploadDocumentInput
				{
					KnowledgeBaseId = KnowVaultConsts.DefaultKnowledgeBaseId,
					Title = "去重测试副本",
					File = new RemoteStreamContent(
						new MemoryStream(System.Text.Encoding.UTF8.GetBytes("第二篇内容")),
						fileName.ToUpperInvariant(),
						"text/markdown")
				}, default);

				await duplicateUpload.Should().ThrowAsync<UserFriendlyException>().WithMessage("*同名文件*");

				var documents = await documentRepository.GetListAsync(x => x.KnowledgeBaseId == KnowVaultConsts.DefaultKnowledgeBaseId && x.SourceFileName == fileName);
				documents.Should().ContainSingle();
			}
			finally
			{
				await documentAppService.DeleteAsync(first.Id, default);
			}
		}
	}
}
