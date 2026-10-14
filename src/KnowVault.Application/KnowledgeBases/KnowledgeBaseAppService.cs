using KnowVault.Application.Contracts;
using KnowVault.Domain;
using KnowVault.Domain.Shared;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;

namespace KnowVault.Application
{
	/// <summary>知识库应用服务（列表 + 默认库 + 增改删；删除只允许空库）。</summary>
	[Authorize]
	public class KnowledgeBaseAppService : KnowVaultAppService, IKnowledgeBaseAppService
	{
		private readonly IRepository<KnowledgeBase, Guid> _knowledgeBaseRepository;
		private readonly IRepository<Document, Guid> _documentRepository;

		/// <summary>构造函数。</summary>
		/// <param name="knowledgeBaseRepository">知识库仓储。</param>
		/// <param name="documentRepository">文档仓储（统计就绪数与判空）。</param>
		public KnowledgeBaseAppService(
			IRepository<KnowledgeBase, Guid> knowledgeBaseRepository,
			IRepository<Document, Guid> documentRepository)
		{
			_knowledgeBaseRepository = knowledgeBaseRepository;
			_documentRepository = documentRepository;
		}

		/// <inheritdoc />
		public async Task<List<KnowledgeBaseDto>> GetListAsync(CancellationToken cancellationToken)
		{
			var knowledgeBases = await _knowledgeBaseRepository.GetListAsync(cancellationToken: cancellationToken);
			var documents = await _documentRepository.GetListAsync(cancellationToken: cancellationToken);

			return knowledgeBases
				.Select(kb => new KnowledgeBaseDto
				{
					Id = kb.Id,
					Name = kb.Name,
					Description = kb.Description,
					ReadyDocumentCount = documents.Count(d => d.KnowledgeBaseId == kb.Id && d.Status == DocumentStatusEnum.Ready)
				})
				// 默认知识库排最前，前端首屏直接选中
				.OrderByDescending(kb => kb.Id == KnowVaultConsts.DefaultKnowledgeBaseId)
				.ThenBy(kb => kb.Name)
				.ToList();
		}

		/// <inheritdoc />
		public async Task<KnowledgeBaseDto?> GetDefaultAsync(CancellationToken cancellationToken)
		{
			var list = await GetListAsync(cancellationToken);
			return list.FirstOrDefault(kb => kb.Id == KnowVaultConsts.DefaultKnowledgeBaseId) ?? list.FirstOrDefault();
		}

		/// <inheritdoc />
		public async Task<KnowledgeBaseDto> CreateAsync(CreateKnowledgeBaseInput input, CancellationToken cancellationToken)
		{
			var knowledgeBase = new KnowledgeBase(
				GuidGenerator.Create(),
				input.Name.Trim(),
				Normalize(input.Description),
				CurrentTenant.Id);

			await _knowledgeBaseRepository.InsertAsync(knowledgeBase, autoSave: true, cancellationToken: cancellationToken);

			return new KnowledgeBaseDto
			{
				Id = knowledgeBase.Id,
				Name = knowledgeBase.Name,
				Description = knowledgeBase.Description,
				ReadyDocumentCount = 0
			};
		}

		/// <inheritdoc />
		public async Task<KnowledgeBaseDto> UpdateAsync(UpdateKnowledgeBaseInput input, CancellationToken cancellationToken)
		{
			var knowledgeBase = await _knowledgeBaseRepository.GetAsync(input.Id, cancellationToken: cancellationToken);
			knowledgeBase.Update(input.Name.Trim(), Normalize(input.Description));
			await _knowledgeBaseRepository.UpdateAsync(knowledgeBase, autoSave: true, cancellationToken: cancellationToken);

			return new KnowledgeBaseDto
			{
				Id = knowledgeBase.Id,
				Name = knowledgeBase.Name,
				Description = knowledgeBase.Description,
				ReadyDocumentCount = await CountReadyDocumentsAsync(knowledgeBase.Id, cancellationToken)
			};
		}

		/// <inheritdoc />
		public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
		{
			if (id == KnowVaultConsts.DefaultKnowledgeBaseId) throw new UserFriendlyException("默认知识库不可删除");

			// 先取一次：不存在时给出「找不到」而不是静默成功
			await _knowledgeBaseRepository.GetAsync(id, cancellationToken: cancellationToken);

			// 文档删除留 P3，因此当前只允许删空库，避免留下无主文档与向量
			var hasDocument = await _documentRepository.AnyAsync(document => document.KnowledgeBaseId == id, cancellationToken: cancellationToken);
			if (hasDocument) throw new UserFriendlyException("知识库下仍有文档，请先清空（文档删除在 P3 提供）");

			await _knowledgeBaseRepository.DeleteAsync(id, autoSave: true, cancellationToken: cancellationToken);
		}

		/// <summary>
		/// 空白描述统一存 null，避免「没填描述」变成空字符串四处判断
		/// </summary>
		/// <param name="value">原始描述</param>
		/// <returns>裁剪后的描述或 null</returns>
		private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

		/// <summary>
		/// 统计某知识库下已就绪的文档数
		/// </summary>
		/// <param name="knowledgeBaseId">知识库 ID</param>
		/// <param name="cancellationToken">取消令牌</param>
		/// <returns>已就绪文档数</returns>
		private async Task<int> CountReadyDocumentsAsync(Guid knowledgeBaseId, CancellationToken cancellationToken)
		{
			var documents = await _documentRepository.GetListAsync(
				document => document.KnowledgeBaseId == knowledgeBaseId,
				cancellationToken: cancellationToken);

			return documents.Count(document => document.Status == DocumentStatusEnum.Ready);
		}
	}
}
