namespace KnowVault.Domain.Shared
{
	/// <summary>
	/// AI 调用类型，用于用量与费用分类
	/// </summary>
	public enum AiOperationEnum
	{
		/// <summary>
		/// 对话生成（回答）
		/// </summary>
		Chat = 0,

		/// <summary>
		/// 文本向量化
		/// </summary>
		Embedding = 1,

		/// <summary>
		/// 候选重排
		/// </summary>
		Rerank = 2,

		/// <summary>
		/// 查询改写
		/// </summary>
		QueryRewrite = 3
	}
}
