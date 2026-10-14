using KnowVault.Application.Contracts;
using KnowVault.Domain;
using KnowVault.Domain.Shared;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Domain.Repositories;

namespace KnowVault.Application
{
	/// <summary>知识库应用服务（P1 只读列表 + 默认库）。</summary>
	[Authorize]
	public class KnowledgeBaseAppService : KnowVaultAppService, IKnowledgeBaseAppService
	{
		private readonly IRepository<KnowledgeBase, Guid> _knowledgeBaseRepository;
		private readonly IRepository<Document, Guid> _documentRepository;

		/// <summary>构造函数。</summary>
		/// <param name="knowledgeBaseRepository">知识库仓储。</param>
		/// <param name="documentRepository">文档仓储（统计就绪数）。</param>
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
	}
}
