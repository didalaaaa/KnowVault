using KnowVault.Application.Contracts;
using KnowVault.Domain;
using Volo.Abp.Domain.Repositories;

namespace KnowVault.Application
{

	/// <summary>
	/// 文档应用服务。P0 仅记录元数据（Pending 待处理）；
	/// 真实上传文件到 MinIO、异步解析切片在 P1。
	/// </summary>
	public class DocumentAppService : KnowVaultAppService, IDocumentAppService
	{
		private readonly IRepository<Document, Guid> _documentRepository;

		public DocumentAppService(IRepository<Document, Guid> documentRepository)
		{
			_documentRepository = documentRepository;
		}

		public async Task<DocumentDto> CreateAsync(CreateDocumentInput input)
		{
			var doc = await _documentRepository.InsertAsync(
				new Document(GuidGenerator.Create(), input.KnowledgeBaseId, input.Title, input.SourceType),
				autoSave: true);

			return new DocumentDto
			{
				Id = doc.Id,
				KnowledgeBaseId = doc.KnowledgeBaseId,
				Title = doc.Title,
				SourceType = doc.SourceType,
				Status = doc.Status,
				CreationTime = doc.CreationTime
			};
		}

		public async Task<List<DocumentDto>> GetListAsync(Guid knowledgeBaseId)
		{
			var docs = await _documentRepository.GetListAsync(x => x.KnowledgeBaseId == knowledgeBaseId);
			return docs
				.Select(d => new DocumentDto
				{
					Id = d.Id,
					KnowledgeBaseId = d.KnowledgeBaseId,
					Title = d.Title,
					SourceType = d.SourceType,
					Status = d.Status,
					ErrorMessage = d.ErrorMessage,
					CreationTime = d.CreationTime
				})
				.ToList();
		}
	}

}
