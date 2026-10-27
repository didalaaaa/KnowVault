using System.Text.Json;
using KnowVault.Domain;
using Xunit;

namespace KnowVault.Tests
{
	/// <summary>
	/// 会话提问范围的持久化测试：主库、范围列表与「全部知识库」标记
	/// </summary>
	public class ConversationScopeTests
	{
		/// <summary>
		/// 更新范围时同步主库，并把完整 ID 列表写成 JSON
		/// </summary>
		[Fact]
		public void UpdateScope_persists_primary_and_id_list()
		{
			var conversation = new Conversation(Guid.NewGuid(), "测试会话", knowledgeBaseId: Guid.NewGuid());
			var first = Guid.NewGuid();
			var second = Guid.NewGuid();

			conversation.UpdateScope(second, [first, second], all: false);

			Assert.Equal(second, conversation.KnowledgeBaseId);
			Assert.False(conversation.ScopeAll);
			Assert.Equal(new[] { first, second }, JsonSerializer.Deserialize<List<Guid>>(conversation.KnowledgeBaseScopeJson!));
		}

		/// <summary>
		/// 「全部知识库」要把标记落库，主库仍按解析结果保存
		/// </summary>
		[Fact]
		public void UpdateScope_marks_all_knowledge_bases()
		{
			var conversation = new Conversation(Guid.NewGuid(), "全局会话");
			var primary = Guid.NewGuid();

			conversation.UpdateScope(primary, [primary], all: true);

			Assert.Equal(primary, conversation.KnowledgeBaseId);
			Assert.True(conversation.ScopeAll);
			Assert.Equal(new[] { primary }, JsonSerializer.Deserialize<List<Guid>>(conversation.KnowledgeBaseScopeJson!));
		}

		/// <summary>
		/// 再次更新范围时覆盖旧值，不追加历史范围
		/// </summary>
		[Fact]
		public void UpdateScope_replaces_previous_scope()
		{
			var conversation = new Conversation(Guid.NewGuid(), "测试会话");
			var oldId = Guid.NewGuid();
			var newId = Guid.NewGuid();

			conversation.UpdateScope(oldId, [oldId], all: false);
			conversation.UpdateScope(newId, [newId], all: false);

			Assert.Equal(newId, conversation.KnowledgeBaseId);
			Assert.False(conversation.ScopeAll);
			Assert.Equal(new[] { newId }, JsonSerializer.Deserialize<List<Guid>>(conversation.KnowledgeBaseScopeJson!));
		}
	}
}
