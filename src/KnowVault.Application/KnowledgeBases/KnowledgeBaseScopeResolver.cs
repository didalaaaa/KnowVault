using KnowVault.Domain;
using KnowVault.Domain.Shared;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace KnowVault.Application
{
	/// <summary>
	/// 一次提问实际使用的知识库范围
	/// </summary>
	public sealed class KnowledgeBaseScope
	{
		/// <summary>
		/// 参与检索的知识库 ID（已去重、校验）
		/// </summary>
		public required IReadOnlyList<Guid> Ids { get; init; }

		/// <summary>
		/// 是否覆盖当前用户可访问的全部知识库
		/// </summary>
		public bool All { get; init; }

		/// <summary>
		/// 主知识库 ID，用于会话列表归组
		/// </summary>
		public required Guid PrimaryId { get; init; }

		/// <summary>
		/// 知识库 ID 到名称的映射，用于引用展示
		/// </summary>
		public required IReadOnlyDictionary<Guid, string> Names { get; init; }
	}

	/// <summary>
	/// 解析并校验一次提问的知识库范围：逐个检查 Viewer 权限，不静默丢弃无权 ID
	/// </summary>
	public class KnowledgeBaseScopeResolver : ITransientDependency
	{
		private readonly IRepository<KnowledgeBase, Guid> _knowledgeBaseRepository;
		private readonly KnowledgeBaseAccessChecker _accessChecker;

		/// <summary>
		/// 构造函数
		/// </summary>
		/// <param name="knowledgeBaseRepository">知识库仓储</param>
		/// <param name="accessChecker">知识库访问检查器</param>
		public KnowledgeBaseScopeResolver(
			IRepository<KnowledgeBase, Guid> knowledgeBaseRepository,
			KnowledgeBaseAccessChecker accessChecker)
		{
			_knowledgeBaseRepository = knowledgeBaseRepository;
			_accessChecker = accessChecker;
		}

		/// <summary>
		/// 解析提问范围
		/// </summary>
		/// <param name="requestedIds">请求指定的知识库 ID；为空时使用默认知识库</param>
		/// <param name="all">是否使用全部可访问知识库</param>
		/// <param name="cancellationToken">取消令牌</param>
		/// <returns>解析后的知识库范围</returns>
		/// <exception cref="UserFriendlyException">范围参数冲突、含无权知识库或结果为空</exception>
		public async Task<KnowledgeBaseScope> ResolveAsync(
			IReadOnlyCollection<Guid>? requestedIds,
			bool all,
			CancellationToken cancellationToken = default)
		{
			if (all && requestedIds is { Count: > 0 })
			{
				throw new UserFriendlyException("「全部知识库」与指定知识库不能同时使用");
			}

			return all
				? await ResolveAllAsync(cancellationToken)
				: await ResolveRequestedAsync(requestedIds, cancellationToken);
		}

		private async Task<KnowledgeBaseScope> ResolveAllAsync(CancellationToken cancellationToken)
		{
			var accessibleIds = await _accessChecker.GetAccessibleIdsAsync(cancellationToken);
			var knowledgeBases = await LoadAsync([.. accessibleIds], cancellationToken);
			var ordered = knowledgeBases
				.OrderByDescending(kb => kb.Id == KnowVaultConsts.DefaultKnowledgeBaseId)
				.ThenBy(kb => kb.Name, StringComparer.Ordinal)
				.ToList();

			if (ordered.Count == 0) throw new UserFriendlyException("没有可提问的知识库");

			var primaryId = ordered[0].Id;
			return new KnowledgeBaseScope
			{
				Ids = [.. ordered.Select(kb => kb.Id)],
				All = true,
				PrimaryId = primaryId,
				Names = ordered.ToDictionary(kb => kb.Id, kb => kb.Name)
			};
		}

		private async Task<KnowledgeBaseScope> ResolveRequestedAsync(
			IReadOnlyCollection<Guid>? requestedIds,
			CancellationToken cancellationToken)
		{
			var requested = new List<Guid>();
			var seen = new HashSet<Guid>();

			foreach (var id in requestedIds ?? [])
			{
				if (id == Guid.Empty || !seen.Add(id)) continue;
				requested.Add(id);
			}

			// 未指定时回落到默认知识库，保持 P4 的单库默认行为
			if (requested.Count == 0) requested.Add(KnowVaultConsts.DefaultKnowledgeBaseId);

			var existing = await LoadAsync(requested, cancellationToken);
			var names = existing.ToDictionary(kb => kb.Id, kb => kb.Name);
			var denied = new List<Guid>();

			foreach (var id in requested)
			{
				try
				{
					await _accessChecker.GetRoleAsync(id, cancellationToken);
				}
				catch (AbpAuthorizationException)
				{
					denied.Add(id);
					continue;
				}

				// 管理员可以访问任意库，但库本身可能已经不存在
				if (!names.ContainsKey(id)) denied.Add(id);
			}

			if (denied.Count > 0)
			{
				var labels = denied.Select(id => names.TryGetValue(id, out var name) ? name : id.ToString());
				throw new UserFriendlyException($"无权访问以下知识库：{string.Join("、", labels)}");
			}

			return new KnowledgeBaseScope
			{
				Ids = requested,
				All = false,
				PrimaryId = requested[0],
				Names = names
			};
		}

		private async Task<List<KnowledgeBase>> LoadAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
		{
			if (ids.Count == 0) return [];

			return await _knowledgeBaseRepository.GetListAsync(
				kb => ids.Contains(kb.Id),
				cancellationToken: cancellationToken);
		}
	}
}
