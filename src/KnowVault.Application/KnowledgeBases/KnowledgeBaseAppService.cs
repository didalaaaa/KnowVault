using KnowVault.Application.Contracts;
using KnowVault.Domain;
using KnowVault.Domain.Shared;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;

namespace KnowVault.Application
{
	/// <summary>知识库应用服务（列表 + 默认库 + 增改删；删除只允许空库）。</summary>
	[Authorize]
	public class KnowledgeBaseAppService : KnowVaultAppService, IKnowledgeBaseAppService
	{
		private readonly IRepository<KnowledgeBase, Guid> _knowledgeBaseRepository;
		private readonly IRepository<KnowledgeBaseMember, Guid> _memberRepository;
		private readonly IRepository<Document, Guid> _documentRepository;
		private readonly KnowledgeBaseAccessChecker _accessChecker;

		/// <summary>构造函数。</summary>
		/// <param name="knowledgeBaseRepository">知识库仓储。</param>
		/// <param name="memberRepository">知识库成员仓储。</param>
		/// <param name="documentRepository">文档仓储（统计就绪数与判空）。</param>
		/// <param name="accessChecker">知识库访问检查器。</param>
		public KnowledgeBaseAppService(
			IRepository<KnowledgeBase, Guid> knowledgeBaseRepository,
			IRepository<KnowledgeBaseMember, Guid> memberRepository,
			IRepository<Document, Guid> documentRepository,
			KnowledgeBaseAccessChecker accessChecker)
		{
			_knowledgeBaseRepository = knowledgeBaseRepository;
			_memberRepository = memberRepository;
			_documentRepository = documentRepository;
			_accessChecker = accessChecker;
		}

		/// <inheritdoc />
		public async Task<List<KnowledgeBaseDto>> GetListAsync(CancellationToken cancellationToken)
		{
			var accessibleIds = await _accessChecker.GetAccessibleIdsAsync(cancellationToken);
			var knowledgeBases = await _knowledgeBaseRepository.GetListAsync(
				kb => accessibleIds.Contains(kb.Id),
				cancellationToken: cancellationToken);
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
		[Authorize(KnowVaultPermissions.KnowledgeBases.Create)]
		public async Task<KnowledgeBaseDto> CreateAsync(CreateKnowledgeBaseInput input, CancellationToken cancellationToken)
		{
			var knowledgeBase = new KnowledgeBase(
				GuidGenerator.Create(),
				input.Name.Trim(),
				Normalize(input.Description),
				CurrentTenant.Id);

			await _knowledgeBaseRepository.InsertAsync(knowledgeBase, autoSave: true, cancellationToken: cancellationToken);

			// 创建者自动成为 Owner，否则新库建完自己都进不去
			var ownerId = CurrentUser.Id ?? throw new AbpAuthorizationException("未登录，无法创建知识库。");
			await _memberRepository.InsertAsync(
				new KnowledgeBaseMember(
					GuidGenerator.Create(),
					knowledgeBase.Id,
					ownerId,
					KnowledgeBaseRoleEnum.Owner,
					CurrentTenant.Id),
				autoSave: true,
				cancellationToken: cancellationToken);

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
			await _accessChecker.EnsureAsync(input.Id, KnowledgeBaseRoleEnum.Owner, cancellationToken);

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

			await _accessChecker.EnsureAsync(id, KnowledgeBaseRoleEnum.Owner, cancellationToken);

			// 先取一次：不存在时给出「找不到」而不是静默成功
			await _knowledgeBaseRepository.GetAsync(id, cancellationToken: cancellationToken);

			// 只允许删空库，避免留下无主文档与向量
			var hasDocument = await _documentRepository.AnyAsync(document => document.KnowledgeBaseId == id, cancellationToken: cancellationToken);
			if (hasDocument) throw new UserFriendlyException("知识库下仍有文档，请先删除库内文档");

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
