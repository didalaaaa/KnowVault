using System.Text;
using KnowVault.Domain.Shared;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Volo.Abp.DependencyInjection;

namespace KnowVault.Ingest
{
	/// <summary>
	/// Markdown 解析器：用 Markdig AST 还原「标题 &gt; 段落」结构。
	/// 只处理标题与段落——列表项本身就是段落，逐个产出即可，避免把 ListBlock 与
	/// 其内部 ParagraphBlock 重复产出（Descendants 是前序遍历，两者都会命中）。
	/// </summary>
	public class MarkdownDocumentParser : IDocumentParser, ITransientDependency
	{
		/// <inheritdoc />
		public DocumentSourceTypeEnum SourceType => DocumentSourceTypeEnum.Markdown;

		/// <inheritdoc />
		public IReadOnlyList<string> Extensions { get; } = [".md", ".markdown"];

		/// <inheritdoc />
		public ParsedDocument Parse(Stream content, string fileName)
		{
			using var reader = new StreamReader(content, leaveOpen: true);
			var markdown = reader.ReadToEnd();
			var document = Markdown.Parse(markdown);
			var result = new ParsedDocument { Title = Path.GetFileNameWithoutExtension(fileName) };

			// 维护 1..6 级标题栈，拼出「一级 > 二级」形式的 HeadingPath
			var headings = new string[7];
			foreach (var block in document.Descendants())
			{
				if (block is HeadingBlock heading && heading.Inline is not null)
				{
					var text = ExtractInlineText(heading.Inline).Trim();
					if (heading.Level is >= 1 and <= 6)
					{
						headings[heading.Level] = text;
						// 更深的层级已失效，必须清空，否则会串到下一个同级标题下
						for (var level = heading.Level + 1; level <= 6; level++)
						{
							headings[level] = string.Empty;
						}
					}

					continue;
				}

				if (block is ParagraphBlock paragraph && paragraph.Inline is not null)
				{
					var text = ExtractInlineText(paragraph.Inline).Trim();
					if (text.Length > 0)
					{
						result.Sections.Add(new ParsedSection
						{
							HeadingPath = BuildHeadingPath(headings),
							Text = text
						});
					}
				}
			}

			return result;
		}

		private static string BuildHeadingPath(string[] headings)
			=> string.Join(" > ", headings.Skip(1).Where(h => !string.IsNullOrWhiteSpace(h)));

		private static string ExtractInlineText(ContainerInline inline)
		{
			var builder = new StringBuilder();
			foreach (var literal in inline.Descendants<LiteralInline>())
			{
				builder.Append(literal.Content.ToString());
			}

			return builder.ToString();
		}
	}
}
