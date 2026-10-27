using System.Text.Json;

namespace KnowVault.Eval
{
	/// <summary>
	/// 金标集加载与校验
	/// </summary>
	public static class GoldenSetLoader
	{
		private static readonly HashSet<string> Categories = ["keyword", "semantic", "cross-document", "unanswerable"];

		/// <summary>
		/// 读取并校验金标集
		/// </summary>
		/// <param name="path">金标集路径</param>
		/// <returns>金标集</returns>
		/// <exception cref="InvalidOperationException">文件缺失或内容不合法</exception>
		public static GoldenSet Load(string path)
		{
			if (!File.Exists(path)) throw new InvalidOperationException($"金标集不存在：{path}");

			var json = File.ReadAllText(path);
			var goldenSet = JsonSerializer.Deserialize<GoldenSet>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
				?? throw new InvalidOperationException($"金标集内容为空或格式非法：{path}");

			Validate(goldenSet);
			return goldenSet;
		}

		private static void Validate(GoldenSet goldenSet)
		{
			if (goldenSet.Questions.Count == 0) throw new InvalidOperationException("金标集没有题目");

			var errors = new List<string>();
			var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			foreach (var question in goldenSet.Questions)
			{
				if (string.IsNullOrWhiteSpace(question.Id)) errors.Add("存在缺少 id 的题目");
				else if (!ids.Add(question.Id)) errors.Add($"题号重复：{question.Id}");

				if (!Categories.Contains(question.Category)) errors.Add($"{question.Id}：未知类别 {question.Category}");
				if (string.IsNullOrWhiteSpace(question.Question)) errors.Add($"{question.Id}：问题为空");
				if (question.Answerable && question.Relevant.Count == 0) errors.Add($"{question.Id}：可回答题缺少金标");
				if (!question.Answerable && question.Relevant.Count > 0) errors.Add($"{question.Id}：不可回答题不应有金标");

				foreach (var relevant in question.Relevant)
				{
					if (string.IsNullOrWhiteSpace(relevant.Document)) errors.Add($"{question.Id}：金标缺少 document");
					if (string.IsNullOrWhiteSpace(relevant.Anchor)) errors.Add($"{question.Id}：金标缺少 anchor");
				}
			}

			if (errors.Count > 0) throw new InvalidOperationException("金标集校验失败：\n - " + string.Join("\n - ", errors));
		}
	}
}
