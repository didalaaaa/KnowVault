namespace KnowVault.Ai
{
	/// <summary>
	/// 加权 RRF 融合：不同分支的分数量纲不可比，只使用排名
	/// </summary>
	public static class ReciprocalRankFusion
	{
		/// <summary>
		/// 融合多条召回分支
		/// </summary>
		/// <param name="branches">召回分支，ID 已按相关性降序</param>
		/// <param name="rrfK">平滑常数</param>
		/// <returns>按融合得分降序排列的候选</returns>
		public static IReadOnlyList<RrfCandidate> Fuse(IReadOnlyList<RrfBranch> branches, int rrfK)
		{
			var k = Math.Max(1, rrfK);
			var scores = new Dictionary<Guid, (double Score, int BestRank)>();

			foreach (var branch in branches)
			{
				var seen = new HashSet<Guid>();

				for (var index = 0; index < branch.Ids.Count; index++)
				{
					var id = branch.Ids[index];

					// 同一分支内重复出现的 ID 只取首次排名，避免人为放大权重
					if (!seen.Add(id)) continue;

					var rank = index + 1;
					var contribution = branch.Weight * branch.QueryWeight / (k + rank);

					if (scores.TryGetValue(id, out var current))
					{
						scores[id] = (current.Score + contribution, Math.Min(current.BestRank, rank));
					}
					else
					{
						scores[id] = (contribution, rank);
					}
				}
			}

			return [.. scores
				.Select(pair => new RrfCandidate
				{
					Id = pair.Key,
					Score = pair.Value.Score,
					BestRank = pair.Value.BestRank
				})
				.OrderByDescending(candidate => candidate.Score)
				.ThenBy(candidate => candidate.BestRank)
				.ThenBy(candidate => candidate.Id)];
		}
	}
}
