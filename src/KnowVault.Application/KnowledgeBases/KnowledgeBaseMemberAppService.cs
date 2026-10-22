using KnowVault.Application.Contracts;
using KnowVault.Domain;
using KnowVault.Domain.Shared;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace KnowVault.Application
{
	/// <summary>
	/// 知识库成员管理：仅 Owner（或系统管理员）可增删改，默认知识库不参与
	/// </summary>
	[Authorize]
	public class KnowledgeBaseMemberAppService : KnowVaultAppService, IKnowledgeBaseMemberAppService
	{
		private readonly IRepository<KnowledgeBaseMember, Guid> _memberRepository;
		private readonly IIdentityUserRepository _userRepository;
		private readonly KnowledgeBaseAccessChecker _accessChecker;

		/// <summary>
		/// 构造函数
		/// </summary>
		/// <param name="memberRepository">成员仓储</param>
		/// <param name="userRepository">用户仓储</param>
		/// <param name="accessChecker">知识库访问检查器</param>
		public KnowledgeBaseMemberAppService(
			IRepository<KnowledgeBaseMember, Guid> memberRepository,
			IIdentityUserRepository userRepository,
			KnowledgeBaseAccessChecker accessChecker)
		{
			_memberRepository = memberRepository;
			_userRepository = userRepository;
			_accessChecker = accessChecker;
		}

		/// <inheritdoc />
		public async Task<List<KnowledgeBaseMemberDto>> GetListAsync(Guid knowledgeBaseId, CancellationToken cancellationToken)
		{
			await _accessChecker.EnsureAsync(knowledgeBaseId, KnowledgeBaseRoleEnum.Viewer, cancellationToken);
			if (knowledgeBaseId == KnowVaultConsts.DefaultKnowledgeBaseId) return [];

			var members = await _memberRepository.GetListAsync(x => x.KnowledgeBaseId == knowledgeBaseId, cancellationToken: cancellationToken);
			if (members.Count == 0) return [];

			var names = new Dictionary<Guid, string>();
			foreach (var userId in members.Select(x => x.UserId).Distinct())
			{
				var user = await _userRepository.FindAsync(userId, cancellationToken: cancellationToken);
				if (user is not null)
				{
					names[userId] = user.UserName ?? user.Id.ToString();
				}
			}

			return [..members
				.OrderByDescending(x => x.Role)
				.ThenBy(x => names[x.UserId])
				.Select(x => new KnowledgeBaseMemberDto
				{
					UserId = x.UserId,
					UserName = names[x.UserId],
					Role = x.Role
				})];
		}

		/// <inheritdoc />
		public async Task<KnowledgeBaseMemberDto> SetAsync(SetKnowledgeBaseMemberInput input, CancellationToken cancellationToken)
		{
			if (input.KnowledgeBaseId == KnowVaultConsts.DefaultKnowledgeBaseId)
			{
				throw new UserFriendlyException("默认知识库对所有登录用户开放，无需配置成员");
			}

			await _accessChecker.EnsureAsync(input.KnowledgeBaseId, KnowledgeBaseRoleEnum.Owner, cancellationToken);
			if (!Enum.IsDefined(input.Role)) throw new UserFriendlyException("无效的知识库角色");

			var user = await _userRepository.GetAsync(input.UserId, cancellationToken: cancellationToken);
			var member = await _memberRepository.FirstOrDefaultAsync(x => x.KnowledgeBaseId == input.KnowledgeBaseId && x.UserId == input.UserId, cancellationToken: cancellationToken);

			if (member is null)
			{
				member = new KnowledgeBaseMember(
					GuidGenerator.Create(),
					input.KnowledgeBaseId,
					input.UserId,
					input.Role,
					CurrentTenant.Id);
				await _memberRepository.InsertAsync(member, autoSave: true, cancellationToken: cancellationToken);
			}
			else
			{
				member.ChangeRole(input.Role);
				await _memberRepository.UpdateAsync(member, autoSave: true, cancellationToken: cancellationToken);
			}

			return new KnowledgeBaseMemberDto
			{
				UserId = member.UserId,
				UserName = user.UserName ?? user.Id.ToString(),
				Role = member.Role
			};
		}

		/// <inheritdoc />
		public async Task RemoveAsync(Guid knowledgeBaseId, Guid userId, CancellationToken cancellationToken)
		{
			if (knowledgeBaseId == KnowVaultConsts.DefaultKnowledgeBaseId)
			{
				throw new UserFriendlyException("默认知识库对所有登录用户开放，无需配置成员");
			}

			await _accessChecker.EnsureAsync(knowledgeBaseId, KnowledgeBaseRoleEnum.Owner, cancellationToken);

			var member = await _memberRepository.FirstOrDefaultAsync(x => x.KnowledgeBaseId == knowledgeBaseId && x.UserId == userId, cancellationToken: cancellationToken);
			if (member is null) return;

			if (member.Role == KnowledgeBaseRoleEnum.Owner)
			{
				var ownerCount = await _memberRepository.CountAsync(x => x.KnowledgeBaseId == knowledgeBaseId && x.Role == KnowledgeBaseRoleEnum.Owner, cancellationToken: cancellationToken);
				if (ownerCount <= 1) throw new UserFriendlyException("至少保留一个知识库 Owner");
			}

			await _memberRepository.DeleteAsync(member, autoSave: true, cancellationToken: cancellationToken);
		}
	}
}
