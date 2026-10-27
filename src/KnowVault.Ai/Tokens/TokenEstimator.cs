namespace KnowVault.Ai
{
	/// <summary>
	/// 轻量 token 估算：模型未返回 usage 时的兜底
	/// </summary>
	public static class TokenEstimator
	{
		/// <summary>
		/// 估算文本的 token 数：CJK 字符按 1 字 1 token，其它非空白字符按 4 字符 1 token 向上取整
		/// </summary>
		/// <param name="text">待估算文本</param>
		/// <returns>估算的 token 数</returns>
		public static int Estimate(string? text)
		{
			if (string.IsNullOrEmpty(text)) return 0;

			var cjkCount = 0;
			var otherCount = 0;

			foreach (var value in text)
			{
				if (char.IsWhiteSpace(value)) continue;

				if (IsCjk(value)) cjkCount++;
				else otherCount++;
			}

			return cjkCount + (otherCount + 3) / 4;
		}

		private static bool IsCjk(char value) =>
			value is >= '\u3400' and <= '\u4DBF' ||
			value is >= '\u4E00' and <= '\u9FFF' ||
			value is >= '\uF900' and <= '\uFAFF';
	}
}
