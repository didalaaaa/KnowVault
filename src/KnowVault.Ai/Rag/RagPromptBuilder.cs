using System.Text;
using System.Text.RegularExpressions;
using KnowVault.Domain.Shared;

namespace KnowVault.Ai
{
	/// <summary>把检索片段与历史轮次组装成系统提示词与多轮消息（纯函数，便于单测）。</summary>
	public static class RagPromptBuilder
	{
		// 引用标记形如 [1]、[12]；只用于剥离助手旧回答，用户问题里的方括号不动
		private static readonly Regex CitationMarker = new(@"\[\d+\]", RegexOptions.Compiled);

		/// <summary>
		/// 组装提示词：历史以独立消息存在，当前问题与资料片段作为最后一条用户消息
		/// </summary>
		/// <param name="question">本轮用户问题。</param>
		/// <param name="chunks">本轮检索到的片段（已按相关性降序）。</param>
		/// <param name="history">历史轮次（时间升序，可为空）。</param>
		/// <param name="systemPromptOverride">知识库自定义系统提示词；为空时用内置模板</param>
		/// <returns>system 提示词与完整消息列表。</returns>
		public static (string System, IReadOnlyList<LlmChatMessage> Messages) Build(
			string question,
			IReadOnlyList<RagRetrievedChunk> chunks,
			IReadOnlyList<LlmChatMessage>? history = null,
			string? systemPromptOverride = null)
		{
			var messages = new List<LlmChatMessage>((history?.Count ?? 0) + 1);
			if (history is not null)
			{
				foreach (var message in history)
				{
					// 助手旧回答里的 [编号] 指向上一轮的片段，留着会让模型把历史编号当成本轮来源
					var content = message.Role == MessageRoleEnum.Assistant ? StripCitationMarkers(message.Content) : message.Content;
					if (string.IsNullOrWhiteSpace(content)) continue;
					messages.Add(new LlmChatMessage { Role = message.Role, Content = content });
				}
			}

			messages.Add(new LlmChatMessage { Role = MessageRoleEnum.User, Content = BuildUserPrompt(question, chunks) });

			// 知识库自定义提示词整体替换内置模板；空白视为未设置
			var system = string.IsNullOrWhiteSpace(systemPromptOverride) ? RagPromptTemplates.SystemPrompt : systemPromptOverride;
			return (system, messages);
		}

		/// <summary>
		/// 组装本轮用户消息（资料片段 + 问题），正文与 P1 保持一致以保证引用编号语义不变
		/// </summary>
		/// <param name="question">用户问题。</param>
		/// <param name="chunks">检索到的片段。</param>
		/// <returns>用户消息正文。</returns>
		private static string BuildUserPrompt(string question, IReadOnlyList<RagRetrievedChunk> chunks)
		{
			var builder = new StringBuilder();
			if (chunks.Count == 0)
			{
				// 明确告知「没有资料」，让模型走「不知道」分支，而不是拿先验知识硬答
				builder.AppendLine("资料片段：没有检索到任何相关资料。");
			}
			else
			{
				builder.AppendLine("资料片段：");
				for (var index = 0; index < chunks.Count; index++)
				{
					var chunk = chunks[index];
					builder.Append('[').Append(index + 1).Append("] 文档《").Append(chunk.DocumentTitle).Append('》');
					if (!string.IsNullOrWhiteSpace(chunk.HeadingPath)) builder.Append(" · ").Append(chunk.HeadingPath);

					builder.AppendLine();
					builder.AppendLine(chunk.Content);
					builder.AppendLine();
				}
			}

			builder.Append("用户问题：").Append(question);
			return builder.ToString();
		}

		/// <summary>
		/// 去掉助手旧回答里的引用标记
		/// </summary>
		/// <param name="content">旧回答正文。</param>
		/// <returns>去掉引用标记并 Trim 后的正文。</returns>
		private static string StripCitationMarkers(string content) => CitationMarker.Replace(content, string.Empty).Trim();
	}
}
