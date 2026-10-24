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
		// 与 IngestOptions.ChunkSizeChars 的默认值保持一致：只为校验重叠上限时做兜底
		private const int DefaultChunkSizeChars = 1000;

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
				.Select(kb => ToDto(kb, documents.Count(d => d.KnowledgeBaseId == kb.Id && d.Status == DocumentStatusEnum.Ready)))
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

			return ToDto(knowledgeBase, 0);
		}

		/// <inheritdoc />
		public async Task<KnowledgeBaseDto> UpdateAsync(UpdateKnowledgeBaseInput input, CancellationToken cancellationToken)
		{
			await _accessChecker.EnsureAsync(input.Id, KnowledgeBaseRoleEnum.Owner, cancellationToken);

			var knowledgeBase = await _knowledgeBaseRepository.GetAsync(input.Id, cancellationToken: cancellationToken);
			knowledgeBase.Update(input.Name.Trim(), Normalize(input.Description));
			await _knowledgeBaseRepository.UpdateAsync(knowledgeBase, autoSave: true, cancellationToken: cancellationToken);

			return ToDto(knowledgeBase, await CountReadyDocumentsAsync(knowledgeBase.Id, cancellationToken));
		}

		/// <inheritdoc />
		public async Task<KnowledgeBaseDto> UpdateSettingsAsync(KnowledgeBaseSettingsInput input, CancellationToken cancellationToken)
		{
			await _accessChecker.EnsureAsync(input.Id, KnowledgeBaseRoleEnum.Owner, cancellationToken);

			var knowledgeBase = await _knowledgeBaseRepository.GetAsync(input.Id, cancellationToken: cancellationToken);
			ValidateSettings(input, knowledgeBase);

			knowledgeBase.UpdateSettings(
				input.ChunkSizeChars,
				input.ChunkOverlapChars,
				input.TopK,
				Normalize(input.SystemPrompt));
			await _knowledgeBaseRepository.UpdateAsync(knowledgeBase, autoSave: true, cancellationToken: cancellationToken);

			return ToDto(knowledgeBase, await CountReadyDocumentsAsync(knowledgeBase.Id, cancellationToken));
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
		/// 校验切片与检索设置；重叠上限取决于「生效后的切片长度」，因此要把知识库现有值一起算进来
		/// </summary>
		/// <param name="input">设置入参</param>
		/// <param name="knowledgeBase">待修改的知识库（提供未填写时的现值）</param>
		/// <exception cref="UserFriendlyException">任一字段越界</exception>
		private static void ValidateSettings(KnowledgeBaseSettingsInput input, KnowledgeBase knowledgeBase)
		{
			var chunkSize = input.ChunkSizeChars;
			if (chunkSize is < 200 or > 4000) throw new UserFriendlyException("切片长度需在 200–4000 字符之间");

			var effectiveSize = chunkSize ?? knowledgeBase.ChunkSizeChars ?? DefaultChunkSizeChars;
			var maxOverlap = Math.Min(500, effectiveSize / 2);
			var overlap = input.ChunkOverlapChars;
			if (overlap < 0 || overlap > maxOverlap) throw new UserFriendlyException($"切片重叠需在 0–{maxOverlap} 字符之间");

			var topK = input.TopK;
			if (topK is < 1 or > 8) throw new UserFriendlyException("检索条数需在 1–8 之间");
		}

		/// <summary>
		/// 实体转 DTO（就绪文档数由调用方算好，列表场景已在内存里统计过）
		/// </summary>
		/// <param name="knowledgeBase">知识库</param>
		/// <param name="readyDocumentCount">已就绪文档数</param>
		/// <returns>知识库 DTO</returns>
		private static KnowledgeBaseDto ToDto(KnowledgeBase knowledgeBase, int readyDocumentCount) => new()
		{
			Id = knowledgeBase.Id,
			Name = knowledgeBase.Name,
			Description = knowledgeBase.Description,
			ReadyDocumentCount = readyDocumentCount,
			ChunkSizeChars = knowledgeBase.ChunkSizeChars,
			ChunkOverlapChars = knowledgeBase.ChunkOverlapChars,
			TopK = knowledgeBase.TopK,
			SystemPrompt = knowledgeBase.SystemPrompt
		};

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
