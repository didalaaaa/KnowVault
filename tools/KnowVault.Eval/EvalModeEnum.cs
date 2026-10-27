namespace KnowVault.Eval
{
	/// <summary>
	/// 评估模式：纯向量、混合检索、混合检索加查询改写
	/// </summary>
	public enum EvalModeEnum
	{
		/// <summary>
		/// 只走向量召回
		/// </summary>
		Vector = 0,

		/// <summary>
		/// 向量加关键词，应用层 RRF 融合
		/// </summary>
		Hybrid = 1,

		/// <summary>
		/// 混合检索前再做查询改写
		/// </summary>
		HybridRewrite = 2
	}
}
