using System.Text.RegularExpressions;

namespace KnowVault.Ai
{
	/// <summary>
	/// 引用标记解析：从回答文本中提取 [n] 形式的引用编号
	/// </summary>
	public static class CitationParser
	{
		private static readonly Regex MarkerPattern = new(@"\[(\d{1,3})\]", RegexOptions.Compiled);

		/// <summary>
		/// 提取回答中的引用编号（1 起、去重、保留首次出现顺序）
		/// </summary>
		/// <param name="answer">模型回答文本</param>
		/// <returns>引用编号列表；[0] 等超出 1 起的编号会被忽略</returns>
		public static IReadOnlyList<int> Parse(string? answer)
		{
			if (string.IsNullOrWhiteSpace(answer)) return [];

			var seen = new HashSet<int>();
			var result = new List<int>();
			foreach (Match match in MarkerPattern.Matches(answer))
			{
				if (!int.TryParse(match.Groups[1].Value, out var index) || index < 1) continue;
				if (seen.Add(index)) result.Add(index);
			}

			return result;
		}
	}
}
