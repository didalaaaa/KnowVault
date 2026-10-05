using KnowVault.Domain;
using KnowVault.Domain.Shared;
using Xunit;

namespace KnowVault.Tests
{
	/// <summary>payload 契约的读写一致性测试。</summary>
	public class ChunkPayloadTests
	{
		/// <summary>Create 产出的字段必须能被 Read* 原样读回。</summary>
		[Fact]
		public void Create_then_read_round_trips()
		{
			var tenantId = Guid.NewGuid();
			var knowledgeBaseId = Guid.NewGuid();
			var documentId = Guid.NewGuid();

			var payload = ChunkPayload.Create(
				tenantId, knowledgeBaseId, documentId, 3, "正文", "标题", "第一章 > 1.1", DocumentSourceTypeEnum.Pdf);

			Assert.Equal(tenantId, ChunkPayload.ReadGuid(payload, ChunkPayload.TenantId));
			Assert.Equal(knowledgeBaseId, ChunkPayload.ReadGuid(payload, ChunkPayload.KnowledgeBaseId));
			Assert.Equal(documentId, ChunkPayload.ReadGuid(payload, ChunkPayload.DocumentId));
			Assert.Equal(3, ChunkPayload.ReadInt(payload, ChunkPayload.ChunkIndex));
			Assert.Equal("正文", ChunkPayload.ReadString(payload, ChunkPayload.Content));
			Assert.Equal("标题", ChunkPayload.ReadString(payload, ChunkPayload.DocumentTitle));
			Assert.Equal("第一章 > 1.1", ChunkPayload.ReadString(payload, ChunkPayload.HeadingPath));
			Assert.Equal((int)DocumentSourceTypeEnum.Pdf, ChunkPayload.ReadInt(payload, ChunkPayload.SourceType));
		}

		/// <summary>payload 必须包含 TECH_STACK 规定的全部字段。</summary>
		[Fact]
		public void Create_contains_all_required_fields()
		{
			var payload = ChunkPayload.Create(
				Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), 0, "c", "t", "", DocumentSourceTypeEnum.Markdown);

			Assert.Equal(8, payload.Count);
		}

		/// <summary>缺少合法 GUID 字段时必须抛错，而不是静默返回 Guid.Empty。</summary>
		[Fact]
		public void ReadGuid_throws_when_missing()
		{
			var payload = new Dictionary<string, object> { [ChunkPayload.DocumentId] = "不是 GUID" };
			Assert.Throws<InvalidOperationException>(() => ChunkPayload.ReadGuid(payload, ChunkPayload.DocumentId));
		}
	}
}
