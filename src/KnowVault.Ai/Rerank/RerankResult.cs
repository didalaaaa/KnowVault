namespace KnowVault.Ai
{
	/// <summary>
	/// 重排结果中的一条：指向输入候选文档的下标与请求内相对相关性分数
	/// </summary>
	public class RerankResult
	{
		/// <summary>
		/// 该结果对应输入候选文档列表的下标（从 0 开始）
		/// </summary>
		public int Index { get; set; }

		/// <summary>
		/// 请求内的相对相关性分数，取值 0~1
		/// 仅供本次请求内排序比较，跨请求不可比（详见 TECH_STACK §9.2）
		/// </summary>
		public double Score { get; set; }
	}
}
