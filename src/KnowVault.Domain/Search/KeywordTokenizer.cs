using System.Text;

namespace KnowVault.Domain
{
	/// <summary>
	/// 中英文混合的轻量切词器：CJK 重叠二元组 + 拉丁/数字词元
	/// </summary>
	public static class KeywordTokenizer
	{
		/// <summary>
		/// 把文本切分为按出现顺序排列的词元
		/// </summary>
		/// <param name="text">任意文本</param>
		/// <returns>已归一化并去除停用词的词元列表</returns>
		public static IReadOnlyList<string> Tokenize(string? text)
		{
			if (string.IsNullOrWhiteSpace(text)) return [];

			var normalized = text.Normalize(NormalizationForm.FormKC).ToLowerInvariant();
			var tokens = new List<string>();
			var index = 0;

			while (index < normalized.Length)
			{
				var current = normalized[index];

				if (IsCjk(current))
				{
					var start = index;
					while (index < normalized.Length && IsCjk(normalized[index])) index++;
					AddCjkSegment(tokens, normalized[start..index]);
					continue;
				}

				if (char.IsAsciiLetterOrDigit(current))
				{
					var start = index;
					while (index < normalized.Length && char.IsAsciiLetterOrDigit(normalized[index])) index++;
					var word = normalized[start..index];
					if (word.Length >= 2 && !KeywordStopWords.Contains(word)) tokens.Add(word);
					continue;
				}

				index++;
			}

			return tokens;
		}

		private static void AddCjkSegment(List<string> tokens, string segment)
		{
			if (segment.Length == 1)
			{
				if (!KeywordStopWords.Contains(segment)) tokens.Add(segment);
				return;
			}

			for (var index = 0; index < segment.Length - 1; index++)
			{
				var token = segment.Substring(index, 2);
				if (!KeywordStopWords.Contains(token)) tokens.Add(token);
			}
		}

		private static bool IsCjk(char value) =>
			value is >= '\u3400' and <= '\u4DBF' ||
			value is >= '\u4E00' and <= '\u9FFF' ||
			value is >= '\uF900' and <= '\uFAFF';
	}
}
