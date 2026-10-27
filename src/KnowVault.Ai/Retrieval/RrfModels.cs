namespace KnowVault.Ai
{
	/// <summary>
	/// 一条召回分支：ID 已按相关性降序排列
	/// </summary>
	public sealed class RrfBranch
	{
		/// <summary>
		/// 按相关性降序排列的切片 ID
		/// </summary>
		public required IReadOnlyList<Guid> Ids { get; init; }

		/// <summary>
		/// 分支权重（dense 或 keyword）
		/// </summary>
		public double Weight { get; init; } = 1.0;

		/// <summary>
		/// 查询权重（原问题 1.0，改写查询见 <see cref="HybridOptions.VariantWeight"/>）
		/// </summary>
		public double QueryWeight { get; init; } = 1.0;
	}

	/// <summary>
	/// 融合后的候选：得分、最佳排名与切片 ID
	/// </summary>
	public sealed class RrfCandidate
	{
		/// <summary>
		/// 切片 ID
		/// </summary>
		public Guid Id { get; init; }

		/// <summary>
		/// 融合得分
		/// </summary>
		public double Score { get; init; }

		/// <summary>
		/// 在所有分支中的最佳排名（从 1 开始）
		/// </summary>
		public int BestRank { get; init; }
	}
}
