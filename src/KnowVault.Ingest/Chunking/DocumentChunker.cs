using System.Text;
using Volo.Abp.DependencyInjection;

namespace KnowVault.Ingest
{
	/// <summary>
	/// 默认切片器：段落优先 + 固定长度上限 + 尾部重叠。
	/// 长度单位是「字符」，用于近似 token（中文没有可靠 tokenizer，见 TECH_STACK §9.1）。
	/// </summary>
	public class DocumentChunker : IDocumentChunker, ITransientDependency
	{
		private static readonly char[] SentenceEndings = ['。', '！', '？', '；', '!', '?', ';', '\n'];

		/// <inheritdoc />
		public IReadOnlyList<TextChunk> Chunk(ParsedDocument document, ChunkOptions options)
		{
			var size = Math.Max(100, options.SizeChars);
			// 重叠不超过容量的一半，否则切片会互相吞没
			var overlap = Math.Clamp(options.OverlapChars, 0, size / 2);
			var chunks = new List<TextChunk>();

			foreach (var section in document.Sections)
			{
				var buffer = new StringBuilder();

				foreach (var block in SplitIntoBlocks(section.Text))
				{
					foreach (var piece in SplitOversizedBlock(block, size))
					{
						if (buffer.Length > 0 && buffer.Length + piece.Length + 1 > size)
						{
							chunks.Add(CreateChunk(chunks.Count, buffer.ToString(), section.HeadingPath));

							// 只有当「重叠段 + 本段」仍放得下时才带重叠，否则宁可断开也不超长
							var tail = TakeTail(buffer.ToString(), overlap);
							buffer.Clear();
							if (tail.Length > 0 && tail.Length + piece.Length + 1 <= size)
							{
								buffer.Append(tail).Append('\n');
							}
						}

						buffer.Append(piece).Append('\n');
					}
				}

				if (buffer.ToString().Trim().Length > 0)
				{
					chunks.Add(CreateChunk(chunks.Count, buffer.ToString(), section.HeadingPath));
				}
			}

			return chunks;
		}

		private static TextChunk CreateChunk(int index, string content, string headingPath) => new()
		{
			Index = index,
			Content = content.Trim(),
			HeadingPath = headingPath
		};

		/// <summary>按空行把段落拆成块，并统一换行符。</summary>
		/// <param name="text">段落正文。</param>
		/// <returns>非空的文本块。</returns>
		private static IEnumerable<string> SplitIntoBlocks(string text)
			=> text.Replace("\r\n", "\n")
				.Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

		/// <summary>把超长块按句子边界切成不超过 <paramref name="size"/> 的片段。</summary>
		/// <param name="block">文本块。</param>
		/// <param name="size">单片最大字符数。</param>
		/// <returns>片段序列。</returns>
		private static IEnumerable<string> SplitOversizedBlock(string block, int size)
		{
			var remaining = block.Trim();
			while (remaining.Length > size)
			{
				var cut = FindCutIndex(remaining, size);
				yield return remaining[..cut].Trim();
				remaining = remaining[cut..].TrimStart();
			}

			if (remaining.Length > 0)
			{
				yield return remaining;
			}
		}

		/// <summary>在容量内寻找最靠后的句子结束符；找不到就硬切。</summary>
		/// <param name="text">文本。</param>
		/// <param name="size">容量。</param>
		/// <returns>切割位置（保留结束符本身）。</returns>
		private static int FindCutIndex(string text, int size)
		{
			for (var index = Math.Min(size, text.Length) - 1; index > size / 4; index--)
			{
				if (SentenceEndings.Contains(text[index]))
				{
					return index + 1;
				}
			}

			return Math.Min(size, text.Length);
		}

		/// <summary>取尾部若干字符作为重叠内容。</summary>
		/// <param name="text">文本。</param>
		/// <param name="overlap">重叠长度。</param>
		/// <returns>尾部片段；文本本身不比重叠长时返回空串。</returns>
		private static string TakeTail(string text, int overlap)
		{
			// 先去掉尾部空白再取：否则换行符会占掉一个重叠字符位
			var trimmed = text.TrimEnd();
			return overlap <= 0 || trimmed.Length <= overlap ? string.Empty : trimmed[^overlap..];
		}
	}
}
