using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace KnowVault.Domain
{
	/// <summary>种子一个默认知识库，保证 P1 的 knowledgeBaseId 维度可用（MVP 不做知识库管理界面）。</summary>
	public class KnowledgeBaseDataSeedContributor : IDataSeedContributor, ITransientDependency
	{
		private readonly IRepository<KnowledgeBase, Guid> _repository;

		/// <summary>构造函数。</summary>
		/// <param name="repository">知识库仓储。</param>
		public KnowledgeBaseDataSeedContributor(IRepository<KnowledgeBase, Guid> repository)
		{
			_repository = repository;
		}

		/// <inheritdoc />
		public async Task SeedAsync(DataSeedContext context)
		{
			var existing = await _repository.FindAsync(KnowVaultConsts.DefaultKnowledgeBaseId);
			if (existing is not null)
			{
				return;
			}

			await _repository.InsertAsync(
				new KnowledgeBase(
					KnowVaultConsts.DefaultKnowledgeBaseId,
					"默认知识库",
					"P1 内置的默认知识库，用于承载上传的文档",
					context.TenantId),
				autoSave: true);
		}
	}
}
