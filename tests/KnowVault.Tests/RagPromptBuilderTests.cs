using KnowVault.Ai;
using Xunit;

namespace KnowVault.Tests
{
	/// <summary>RAG 提示词组装测试。</summary>
	public class RagPromptBuilderTests
	{
		/// <summary>片段要带 [编号]、文档标题与标题路径。</summary>
		[Fact]
		public void Build_numbers_chunks_with_title_and_heading()
		{
			var chunks = new List<RagRetrievedChunk>
			{
				new() { DocumentTitle = "架构说明", HeadingPath = "第三章 > 3.2", Content = "第一段资料", Score = 0.9 },
				new() { DocumentTitle = "运维手册", HeadingPath = "", Content = "第二段资料", Score = 0.8 }
			};

			var (system, user) = RagPromptBuilder.Build("怎么部署？", chunks);

			// 断言系统提示词的硬约束：只允许依据资料作答（别写成语义相近但实际不存在的子串）
			Assert.Contains("只使用资料片段中的信息作答", system);
			Assert.Contains("[1] 文档《架构说明》 · 第三章 > 3.2", user);
			Assert.Contains("[2] 文档《运维手册》", user);
			Assert.Contains("第一段资料", user);
			Assert.Contains("用户问题：怎么部署？", user);
		}

		/// <summary>没有命中任何片段时必须明说「没有资料」，让模型走不知道分支。</summary>
		[Fact]
		public void Build_when_no_chunk_says_no_material()
		{
			var (_, user) = RagPromptBuilder.Build("无关问题", []);

			Assert.Contains("没有检索到任何相关资料", user);
			Assert.DoesNotContain("[1]", user);
		}
	}
}
