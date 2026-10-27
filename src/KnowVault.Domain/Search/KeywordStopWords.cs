namespace KnowVault.Domain
{
	/// <summary>
	/// 关键词检索使用的最小停用词表，只收高频虚词与疑问词
	/// </summary>
	public static class KeywordStopWords
	{
		private static readonly HashSet<string> Words = new(StringComparer.Ordinal)
		{
			"的", "了", "是", "在", "和", "与", "及", "或",
			"我们", "这个", "哪些", "什么", "怎么", "如何", "为什么",
			"可以", "需要", "一个", "进行", "通过", "以及"
		};

		/// <summary>
		/// 判断词元是否应被丢弃
		/// </summary>
		/// <param name="token">归一化后的词元</param>
		/// <returns>命中停用词返回 <c>true</c></returns>
		public static bool Contains(string token) => Words.Contains(token);
	}
}
