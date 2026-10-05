using System.Text;

namespace KnowVault.Ai
{
	/// <summary>把检索片段与问题组装成 system/user 两段提示词（纯函数，便于单测）。</summary>
	public static class RagPromptBuilder
	{
		/// <summary>组装提示词。</summary>
		/// <param name="question">用户问题。</param>
		/// <param name="chunks">检索到的片段（已按相似度降序）。</param>
		/// <returns>system 与 user 提示词。</returns>
		public static (string System, string User) Build(string question, IReadOnlyList<RagRetrievedChunk> chunks)
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
					if (!string.IsNullOrWhiteSpace(chunk.HeadingPath))
					{
						builder.Append(" · ").Append(chunk.HeadingPath);
					}

					builder.AppendLine();
					builder.AppendLine(chunk.Content);
					builder.AppendLine();
				}
			}

			builder.Append("用户问题：").Append(question);
			return (RagPromptTemplates.SystemPrompt, builder.ToString());
		}
	}
}
