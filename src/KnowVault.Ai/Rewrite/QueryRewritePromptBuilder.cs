using System.Text.RegularExpressions;
using KnowVault.Domain.Shared;

namespace KnowVault.Ai
{
	/// <summary>
	/// 查询改写的提示词：只产检索表述，不回答问题
	/// </summary>
	public static class QueryRewritePromptBuilder
	{
		private static readonly Regex CitationPattern = new(@"\[\d+\]", RegexOptions.Compiled);

		/// <summary>
		/// 构建改写调用所需的系统提示词与消息
		/// </summary>
		/// <param name="question">原问题</param>
		/// <param name="history">最近的历史消息（时间升序）</param>
		/// <param name="maxVariants">最多生成的变体数</param>
		/// <returns>系统提示词与消息列表</returns>
		public static (string SystemPrompt, IReadOnlyList<LlmChatMessage> Messages) Build(
			string question,
			IReadOnlyList<LlmChatMessage> history,
			int maxVariants)
		{
			var messages = new List<LlmChatMessage>();

			// 只带最近两轮，且历史里的引用编号对检索没有意义，先剥掉
			foreach (var message in history.TakeLast(2))
			{
				messages.Add(new LlmChatMessage
				{
					Role = message.Role,
					Content = CitationPattern.Replace(message.Content, string.Empty).Trim()
				});
			}

			messages.Add(new LlmChatMessage
			{
				Role = MessageRoleEnum.User,
				Content = question
			});

			return ($"""
				你是检索查询改写助手。根据用户问题生成最多 {maxVariants} 条用于知识库检索的替代表述，覆盖同义词、缩写和英文术语。
				要求：
				1. 不要回答问题，不要解释，不要输出 Markdown；
				2. 只输出 JSON 字符串数组，例如 ["表述一","表述二"]；
				3. 每条表述语义完整、可独立检索，不要重复原问题。
				""", messages);
		}
	}
}
