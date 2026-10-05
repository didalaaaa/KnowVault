using KnowVault.Ingest;
using Microsoft.Extensions.Options;
using Xunit;

namespace KnowVault.Tests
{
	/// <summary>切片器行为测试（纯函数、无 IO）。</summary>
	public class DocumentChunkerTests
	{
		private static DocumentChunker CreateChunker(int size = 100, int overlap = 20)
			=> new(Options.Create(new IngestOptions { ChunkSizeChars = size, ChunkOverlapChars = overlap }));

		private static ParsedDocument Document(params (string Heading, string Text)[] sections)
		{
			var document = new ParsedDocument { Title = "测试文档" };
			foreach (var (heading, text) in sections)
			{
				document.Sections.Add(new ParsedSection { HeadingPath = heading, Text = text });
			}

			return document;
		}

		/// <summary>短文档只产出一个切片，正文不丢字。</summary>
		[Fact]
		public void Short_document_produces_single_chunk()
		{
			var chunks = CreateChunker().Chunk(Document(("", "很短的一段话。")));

			Assert.Single(chunks);
			Assert.Equal(0, chunks[0].Index);
			Assert.Contains("很短的一段话。", chunks[0].Content);
		}

		/// <summary>多个短段落在容量允许时应合并进同一个切片。</summary>
		[Fact]
		public void Small_blocks_are_merged()
		{
			var text = string.Join("\n\n", Enumerable.Repeat("十字符左右的一段。", 3));
			var chunks = CreateChunker(size: 200).Chunk(Document(("", text)));

			Assert.Single(chunks);
		}

		/// <summary>超出容量的段落必须切分为多个切片，且每个切片不超长。</summary>
		[Fact]
		public void Every_chunk_respects_size_limit()
		{
			var text = string.Join("\n\n", Enumerable.Repeat(new string('测', 80), 6));
			var chunks = CreateChunker(size: 100, overlap: 20).Chunk(Document(("", text)));

			Assert.True(chunks.Count > 1);
			Assert.All(chunks, c => Assert.True(c.Content.Length <= 100, $"切片长度 {c.Content.Length} 超过 100"));
		}

		/// <summary>相邻切片之间要带重叠内容（避免跨界语义断裂）。</summary>
		[Fact]
		public void Consecutive_chunks_overlap()
		{
			// 取 50 字符的段落：容量 100、重叠 20 时，「重叠段 + 本段」正好放得下，重叠才会真正生效
			var first = new string('甲', 50);
			var second = new string('乙', 50);
			var chunks = CreateChunker(size: 100, overlap: 20).Chunk(Document(("", $"{first}\n\n{second}")));

			Assert.Equal(2, chunks.Count);
			Assert.Contains(new string('甲', 20), chunks[1].Content);
		}

		/// <summary>超长单段落要按句子边界硬切，不产生超长切片。</summary>
		[Fact]
		public void Oversized_block_is_hard_split()
		{
			var sentence = new string('字', 45) + "。";
			var text = string.Concat(Enumerable.Repeat(sentence, 10));
			var chunks = CreateChunker(size: 100, overlap: 20).Chunk(Document(("", text)));

			Assert.True(chunks.Count >= 5);
			Assert.All(chunks, c => Assert.True(c.Content.Length <= 100));
		}

		/// <summary>切片的 HeadingPath 必须继承所属段落的标题路径。</summary>
		[Fact]
		public void Chunks_inherit_heading_path()
		{
			var chunks = CreateChunker(size: 60)
				.Chunk(Document(("第一章", new string('甲', 200)), ("第二章", new string('乙', 200))));

			Assert.Contains(chunks, c => c.HeadingPath == "第一章");
			Assert.Contains(chunks, c => c.HeadingPath == "第二章");
		}

		/// <summary>空文档产出 0 个切片，不能凭空造内容。</summary>
		[Fact]
		public void Empty_document_produces_no_chunk()
		{
			Assert.Empty(CreateChunker().Chunk(Document(("", "   "), ("标题", ""))));
		}
	}
}
