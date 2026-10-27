namespace KnowVault.Ai
{
	/// <summary>
	/// 查询改写参数
	/// </summary>
	public class QueryRewriteOptions
	{
		/// <summary>
		/// 最多生成的变体数，实际取值会被收敛到 0–2
		/// </summary>
		public int MaxVariants { get; set; } = 2;

		/// <summary>
		/// 触发改写的最短问题长度（字符数）
		/// </summary>
		public int MinQuestionLength { get; set; } = 6;

		/// <summary>
		/// 改写调用的最大输出 token 数
		/// </summary>
		public int MaxTokens { get; set; } = 200;
	}
}
