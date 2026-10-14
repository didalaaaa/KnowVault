namespace KnowVault.Application.Contracts
{
	/// <summary>
	/// 流式问答事件类型
	/// </summary>
	public enum QaStreamEventKindEnum
	{
		/// <summary>
		/// 首帧：会话已就绪且检索/重排完成，携带会话 ID 与引用
		/// </summary>
		Started = 0,

		/// <summary>
		/// 增量回答文本
		/// </summary>
		Delta = 1,

		/// <summary>
		/// 末帧：回答已生成并落库
		/// </summary>
		Completed = 2
	}
}
