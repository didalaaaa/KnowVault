namespace KnowVault.Ai
{
	/// <summary>
	/// 混合检索参数：dense + sparse 双路召回后用加权 RRF 融合
	/// </summary>
	public class HybridOptions
	{
		/// <summary>
		/// 向量分支权重，取值会被收敛到 0–3
		/// </summary>
		public double DenseWeight { get; set; } = 1.0;

		/// <summary>
		/// 关键词分支权重，取值会被收敛到 0–3
		/// </summary>
		public double KeywordWeight { get; set; } = 1.0;

		/// <summary>
		/// 改写查询的权重（原问题固定 1.0），取值会被收敛到 0–3
		/// </summary>
		public double VariantWeight { get; set; } = 0.8;

		/// <summary>
		/// RRF 平滑常数，取值会被收敛到 10–100
		/// </summary>
		public int RrfK { get; set; } = 60;

		/// <summary>
		/// 每条查询每个分支的召回条数，取值会被收敛到 5–30
		/// </summary>
		public int PerQueryCandidateCount { get; set; } = 10;
	}
}
