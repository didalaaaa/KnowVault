using KnowVault.Ai;
using KnowVault.Domain.Shared;
using Xunit;

namespace KnowVault.Tests
{
	/// <summary>
	/// RAG 提示词组装测试
	/// </summary>
	public class RagPromptBuilderTests
	{
		/// <summary>
		/// 片段要带 [编号]、文档标题与标题路径
		/// </summary>
		[Fact]
		public void Build_numbers_chunks_with_title_and_heading()
		{
			var chunks = new List<RagRetrievedChunk>
			{
				new() { DocumentTitle = "架构说明", HeadingPath = "第三章 > 3.2", Content = "第一段资料", Score = 0.9 },
				new() { DocumentTitle = "运维手册", HeadingPath = "", Content = "第二段资料", Score = 0.8 }
			};

			var (system, messages) = RagPromptBuilder.Build("怎么部署？", chunks);
			var user = Assert.Single(messages).Content;

			// 断言系统提示词的硬约束：只允许依据资料作答（别写成语义相近但实际不存在的子串）
			Assert.Contains("只使用资料片段中的信息作答", system);
			Assert.Contains("[1] 文档《架构说明》 · 第三章 > 3.2", user);
			Assert.Contains("[2] 文档《运维手册》", user);
			Assert.Contains("第一段资料", user);
			Assert.Contains("用户问题：怎么部署？", user);
		}

		/// <summary>
		/// 没有命中任何片段时必须明说「没有资料」，让模型走不知道分支
		/// </summary>
		[Fact]
		public void Build_when_no_chunk_says_no_material()
		{
			var (_, messages) = RagPromptBuilder.Build("无关问题", []);

			var user = Assert.Single(messages).Content;
			Assert.Contains("没有检索到任何相关资料", user);
			Assert.DoesNotContain("[1]", user);
		}

		/// <summary>
		/// 历史按时间顺序排在当前提问之前（指代消解靠它）
		/// </summary>
		[Fact]
		public void Build_puts_history_before_current_question()
		{
			var history = new List<LlmChatMessage>
			{
				new() { Role = MessageRoleEnum.User, Content = "什么是 Qdrant？" },
				new() { Role = MessageRoleEnum.Assistant, Content = "Qdrant 是向量库。" }
			};

			var (_, messages) = RagPromptBuilder.Build("它支持过滤吗？", [], history);

			Assert.Equal(3, messages.Count);
			Assert.Equal(MessageRoleEnum.User, messages[0].Role);
			Assert.Equal("什么是 Qdrant？", messages[0].Content);
			Assert.Equal(MessageRoleEnum.Assistant, messages[1].Role);
			Assert.Equal(MessageRoleEnum.User, messages[2].Role);
			Assert.Contains("用户问题：它支持过滤吗？", messages[2].Content);
		}

		/// <summary>
		/// 助手旧回答里的 [编号] 必须剥离，否则模型会把上轮编号当成本轮来源
		/// </summary>
		[Fact]
		public void Build_strips_citation_markers_from_assistant_history()
		{
			var history = new List<LlmChatMessage>
			{
				new() { Role = MessageRoleEnum.Assistant, Content = "结论是 A[1][2]。" }
			};

			var (_, messages) = RagPromptBuilder.Build("继续", [], history);

			Assert.Equal("结论是 A。", messages[0].Content);
		}

		/// <summary>
		/// 用户历史消息里的方括号不能被当成引用标记剥掉
		/// </summary>
		[Fact]
		public void Build_keeps_user_history_content_intact()
		{
			var history = new List<LlmChatMessage>
			{
				new() { Role = MessageRoleEnum.User, Content = "看下 [1] 这条规则" }
			};

			var (_, messages) = RagPromptBuilder.Build("继续", [], history);

			Assert.Equal("看下 [1] 这条规则", messages[0].Content);
		}

		/// <summary>
		/// 空白的助手历史消息要丢弃，避免空壳消息进提示词
		/// </summary>
		[Fact]
		public void Build_drops_empty_assistant_history_messages()
		{
			var history = new List<LlmChatMessage>
			{
				new() { Role = MessageRoleEnum.User, Content = "   " },
				new() { Role = MessageRoleEnum.Assistant, Content = "[1]" }
			};

			var (_, messages) = RagPromptBuilder.Build("继续", [], history);

			Assert.Single(messages);
			Assert.Equal(MessageRoleEnum.User, messages[0].Role);
			Assert.Contains("用户问题：继续", messages[0].Content);
		}
	}
}
