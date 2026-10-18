using KnowVault.Domain;
using KnowVault.Domain.Shared;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Users;

namespace KnowVault.Application
{
	/// <summary>
	/// 知识库级访问控制：系统管理员短路，默认知识库对登录用户开放，其余按成员角色判断
	/// </summary>
	public class KnowledgeBaseAccessChecker : ITransientDependency
	{
		private readonly IRepository<KnowledgeBase, Guid> _knowledgeBaseRepository;
		private readonly IRepository<KnowledgeBaseMember, Guid> _memberRepository;
		private readonly ICurrentUser _currentUser;
		private readonly IPermissionChecker _permissionChecker;

		/// <summary>
		/// 构造函数
		/// </summary>
		/// <param name="knowledgeBaseRepository">知识库仓储</param>
		/// <param name="memberRepository">成员仓储</param>
		/// <param name="currentUser">当前用户</param>
		/// <param name="permissionChecker">权限检查器</param>
		public KnowledgeBaseAccessChecker(
			IRepository<KnowledgeBase, Guid> knowledgeBaseRepository,
			IRepository<KnowledgeBaseMember, Guid> memberRepository,
			ICurrentUser currentUser,
			IPermissionChecker permissionChecker)
		{
			_knowledgeBaseRepository = knowledgeBaseRepository;
			_memberRepository = memberRepository;
			_currentUser = currentUser;
			_permissionChecker = permissionChecker;
		}

		/// <summary>
		/// 取当前用户在指定知识库的角色
		/// </summary>
		/// <param name="knowledgeBaseId">知识库 ID</param>
		/// <param name="cancellationToken">取消令牌</param>
		/// <returns>库内角色</returns>
		/// <exception cref="AbpAuthorizationException">无权访问该知识库</exception>
		public async Task<KnowledgeBaseRoleEnum> GetRoleAsync(Guid knowledgeBaseId, CancellationToken cancellationToken = default)
		{
			if (await IsAdminAsync()) return KnowledgeBaseRoleEnum.Owner;

			// 默认库是 demo 的公共库：所有登录用户都可写，避免演示时先配成员
			if (knowledgeBaseId == KnowVaultConsts.DefaultKnowledgeBaseId)
			{
				return KnowledgeBaseRoleEnum.Editor;
			}

			var userId = _currentUser.Id ?? throw new AbpAuthorizationException("未登录，无法访问知识库。");
			var member = await _memberRepository.FirstOrDefaultAsync(
				x => x.KnowledgeBaseId == knowledgeBaseId && x.UserId == userId,
				cancellationToken: cancellationToken);

			return member?.Role ?? throw new AbpAuthorizationException("无权访问该知识库。");
		}

		/// <summary>
		/// 校验当前用户至少具备指定角色
		/// </summary>
		/// <param name="knowledgeBaseId">知识库 ID</param>
		/// <param name="required">最低角色</param>
		/// <param name="cancellationToken">取消令牌</param>
		/// <returns>任务</returns>
		/// <exception cref="AbpAuthorizationException">角色不足</exception>
		public async Task EnsureAsync(Guid knowledgeBaseId, KnowledgeBaseRoleEnum required, CancellationToken cancellationToken = default)
		{
			var role = await GetRoleAsync(knowledgeBaseId, cancellationToken);
			if (role < required)
			{
				throw new AbpAuthorizationException("当前角色无权执行该操作。");
			}
		}

		/// <summary>
		/// 取当前用户可见的知识库 ID：默认库 + 成员库；管理员返回全部
		/// </summary>
		/// <param name="cancellationToken">取消令牌</param>
		/// <returns>可见知识库 ID 集合</returns>
		public async Task<HashSet<Guid>> GetAccessibleIdsAsync(CancellationToken cancellationToken = default)
		{
			if (await IsAdminAsync())
			{
				var all = await _knowledgeBaseRepository.GetListAsync(cancellationToken: cancellationToken);
				return [.. all.Select(x => x.Id)];
			}

			var result = new HashSet<Guid> { KnowVaultConsts.DefaultKnowledgeBaseId };
			if (_currentUser.Id is not { } userId) return result;

			var members = await _memberRepository.GetListAsync(
				x => x.UserId == userId,
				cancellationToken: cancellationToken);

			foreach (var member in members)
			{
				result.Add(member.KnowledgeBaseId);
			}

			return result;
		}

		private async Task<bool> IsAdminAsync()
		{
			return await _permissionChecker.IsGrantedAsync(KnowVaultPermissions.KnowledgeBases.Manage);
		}
	}
}
