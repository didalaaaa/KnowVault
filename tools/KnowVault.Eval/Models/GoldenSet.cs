namespace KnowVault.Eval
{
	/// <summary>
	/// 金标集根对象
	/// </summary>
	public class GoldenSet
	{
		/// <summary>
		/// 金标集版本
		/// </summary>
		public string Version { get; set; } = string.Empty;

		/// <summary>
		/// 说明
		/// </summary>
		public string Description { get; set; } = string.Empty;

		/// <summary>
		/// 题目列表
		/// </summary>
		public List<GoldenQuestion> Questions { get; set; } = [];
	}

	/// <summary>
	/// 一道金标题
	/// </summary>
	public class GoldenQuestion
	{
		/// <summary>
		/// 题号
		/// </summary>
		public string Id { get; set; } = string.Empty;

		/// <summary>
		/// 类别：keyword / semantic / cross-document / unanswerable
		/// </summary>
		public string Category { get; set; } = string.Empty;

		/// <summary>
		/// 问题文本
		/// </summary>
		public string Question { get; set; } = string.Empty;

		/// <summary>
		/// 金标切片对应的原文锚点
		/// </summary>
		public List<GoldenRelevant> Relevant { get; set; } = [];

		/// <summary>
		/// 回答要点，供人工核对
		/// </summary>
		public List<string> AnswerPoints { get; set; } = [];

		/// <summary>
		/// 语料是否能回答该题
		/// </summary>
		public bool Answerable { get; set; }
	}

	/// <summary>
	/// 一条金标锚点
	/// </summary>
	public class GoldenRelevant
	{
		/// <summary>
		/// 语料文件名（含扩展名）
		/// </summary>
		public string Document { get; set; } = string.Empty;

		/// <summary>
		/// 原文中 10 到 30 字的连续片段
		/// </summary>
		public string Anchor { get; set; } = string.Empty;
	}
}
