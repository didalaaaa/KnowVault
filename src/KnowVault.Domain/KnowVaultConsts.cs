using Volo.Abp.Identity;

namespace KnowVault.Domain
{

	public static class KnowVaultConsts
	{
	    public const string DbTablePrefix = "App";
	    public const string? DbSchema = null;
	    public const string AdminEmailDefaultValue = IdentityDataSeedContributor.AdminEmailDefaultValue;
	    public const string AdminPasswordDefaultValue = "1q2w3E*";

	    /// <summary>P1 内置默认知识库的固定 ID（MVP 不做知识库管理界面，前端默认选它）。</summary>
	    public static readonly Guid DefaultKnowledgeBaseId = Guid.Parse("00000000-0000-0000-0000-000000000001");
	}

}
