namespace KnowVault.Ai
{
	/// <summary>
	/// 检索排序指标：Recall@K、MRR@K、nDCG@K（二值相关性）
	/// </summary>
	public static class RankingMetrics
	{
		/// <summary>
		/// 计算 Recall@K：top-K 命中的金标切片占全部金标切片的比例
		/// </summary>
		/// <param name="retrieved">检索结果（按相关性降序）</param>
		/// <param name="gold">金标切片 ID 集合</param>
		/// <param name="k">截断深度</param>
		/// <returns>命中比例；金标为空时返回 0</returns>
		public static double RecallAtK(IReadOnlyCollection<Guid> retrieved, IReadOnlyCollection<Guid> gold, int k)
		{
			if (gold.Count == 0 || k <= 0) return 0;

			var goldSet = new HashSet<Guid>(gold);
			var hits = retrieved.Take(k).Count(goldSet.Contains);
			return (double)hits / gold.Count;
		}

		/// <summary>
		/// 计算 MRR@K：第一个命中的倒数排名
		/// </summary>
		/// <param name="retrieved">检索结果（按相关性降序）</param>
		/// <param name="gold">金标切片 ID 集合</param>
		/// <param name="k">截断深度</param>
		/// <returns>倒数排名；前 K 条都没有命中时返回 0</returns>
		public static double MrrAtK(IReadOnlyCollection<Guid> retrieved, IReadOnlyCollection<Guid> gold, int k)
		{
			if (gold.Count == 0 || k <= 0) return 0;

			var goldSet = new HashSet<Guid>(gold);
			var rank = 1;
			foreach (var id in retrieved.Take(k))
			{
				if (goldSet.Contains(id)) return 1.0 / rank;
				rank++;
			}

			return 0;
		}

		/// <summary>
		/// 计算 nDCG@K：二值相关性下的归一化折损累计增益
		/// </summary>
		/// <param name="retrieved">检索结果（按相关性降序）</param>
		/// <param name="gold">金标切片 ID 集合</param>
		/// <param name="k">截断深度</param>
		/// <returns>归一化增益；金标为空时返回 0</returns>
		public static double NdcgAtK(IReadOnlyCollection<Guid> retrieved, IReadOnlyCollection<Guid> gold, int k)
		{
			if (gold.Count == 0 || k <= 0) return 0;

			var goldSet = new HashSet<Guid>(gold);
			var dcg = 0.0;
			var rank = 1;
			foreach (var id in retrieved.Take(k))
			{
				if (goldSet.Contains(id)) dcg += 1.0 / Math.Log2(rank + 1);
				rank++;
			}

			var idealHits = Math.Min(gold.Count, k);
			var idcg = 0.0;
			for (var i = 1; i <= idealHits; i++) idcg += 1.0 / Math.Log2(i + 1);

			return idcg == 0 ? 0 : dcg / idcg;
		}
	}
}
