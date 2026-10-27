using System.Text.Json;

namespace KnowVault.Ai
{
	/// <summary>
	/// 解析改写模型返回的 JSON 字符串数组，任何非法输出都退化为空列表
	/// </summary>
	public static class QueryRewriteParser
	{
		private const int MinLength = 2;
		private const int MaxLength = 120;

		/// <summary>
		/// 解析并清洗变体查询
		/// </summary>
		/// <param name="content">模型返回的文本</param>
		/// <param name="originalQuestion">原问题（用于去重）</param>
		/// <param name="maxVariants">最多保留的变体数</param>
		/// <returns>清洗后的变体列表；解析失败时为空列表</returns>
		public static IReadOnlyList<string> Parse(string? content, string originalQuestion, int maxVariants)
		{
			if (string.IsNullOrWhiteSpace(content) || maxVariants <= 0) return [];

			var json = ExtractJsonArray(content);
			if (json is null) return [];

			string[]? items;
			try
			{
				items = JsonSerializer.Deserialize<string[]>(json);
			}
			catch (JsonException)
			{
				return [];
			}

			if (items is null) return [];

			var original = originalQuestion.Trim();
			var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { original };
			var result = new List<string>();

			foreach (var item in items)
			{
				var value = item?.Trim();
				if (string.IsNullOrEmpty(value)) continue;
				if (value.Length < MinLength || value.Length > MaxLength) continue;
				if (!seen.Add(value)) continue;

				result.Add(value);
				if (result.Count >= maxVariants) break;
			}

			return result;
		}

		private static string? ExtractJsonArray(string content)
		{
			var text = content.Trim();

			// 允许 ```json ... ``` 包裹；模型偶尔会在数组前后带一句解释
			if (text.StartsWith("```", StringComparison.Ordinal))
			{
				var firstLineEnd = text.IndexOf('\n');
				if (firstLineEnd >= 0) text = text[(firstLineEnd + 1)..];
				if (text.EndsWith("```", StringComparison.Ordinal)) text = text[..^3];
				text = text.Trim();
			}

			var start = text.IndexOf('[');
			var end = text.LastIndexOf(']');

			return start >= 0 && end > start ? text[start..(end + 1)] : null;
		}
	}
}
