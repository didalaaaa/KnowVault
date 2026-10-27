namespace KnowVault.Domain
{
	/// <summary>
	/// AI 用量记录器：写入失败不得影响问答与摄取主流程
	/// </summary>
	public interface IAiUsageRecorder
	{
		/// <summary>
		/// 记录一次 AI 调用
		/// </summary>
		/// <param name="entry">用量条目</param>
		/// <param name="cancellationToken">取消令牌</param>
		/// <returns></returns>
		Task RecordAsync(AiUsageEntry entry, CancellationToken cancellationToken = default);
	}
}
