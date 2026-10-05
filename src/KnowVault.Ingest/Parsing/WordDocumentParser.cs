using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using KnowVault.Domain.Shared;
using Volo.Abp.DependencyInjection;

namespace KnowVault.Ingest
{
	/// <summary>Word(.docx) 解析器：按 Heading1-6 样式还原标题层级，表格按行拼成文本。</summary>
	public class WordDocumentParser : IDocumentParser, ITransientDependency
	{
		/// <inheritdoc />
		public DocumentSourceTypeEnum SourceType => DocumentSourceTypeEnum.Word;

		/// <inheritdoc />
		public IReadOnlyList<string> Extensions { get; } = [".docx"];

		/// <inheritdoc />
		public ParsedDocument Parse(Stream content, string fileName)
		{
			using var document = WordprocessingDocument.Open(content, false);
			var body = document.MainDocumentPart?.Document?.Body
					   ?? throw new InvalidOperationException("docx 缺少正文内容。");

			var result = new ParsedDocument { Title = Path.GetFileNameWithoutExtension(fileName) };
			var headings = new string[7];

			foreach (var element in body.Elements())
			{
				switch (element)
				{
					case Paragraph paragraph:
						var text = ReadText(paragraph);
						if (text.Length == 0)
						{
							break;
						}

						var level = GetHeadingLevel(paragraph);
						if (level > 0)
						{
							headings[level] = text;
							for (var deeper = level + 1; deeper <= 6; deeper++)
							{
								headings[deeper] = string.Empty;
							}

							break;
						}

						result.Sections.Add(new ParsedSection
						{
							HeadingPath = BuildHeadingPath(headings),
							Text = text
						});
						break;

					case Table table:
						foreach (var row in table.Elements<TableRow>())
						{
							var cells = row.Elements<TableCell>().Select(ReadText).ToList();
							if (cells.Any(c => c.Length > 0))
							{
								result.Sections.Add(new ParsedSection
								{
									HeadingPath = BuildHeadingPath(headings),
									Text = string.Join(" | ", cells)
								});
							}
						}

						break;
				}
			}

			return result;
		}

		private static string ReadText(OpenXmlElement element)
			=> string.Concat(element.Descendants<Text>().Select(t => t.Text)).Trim();

		private static string BuildHeadingPath(string[] headings)
			=> string.Join(" > ", headings.Skip(1).Where(h => !string.IsNullOrWhiteSpace(h)));

		/// <summary>从段落样式名推断标题级别。</summary>
		/// <param name="paragraph">段落。</param>
		/// <returns>1–6 表示标题级别；0 表示正文。</returns>
		private static int GetHeadingLevel(Paragraph paragraph)
		{
			var styleId = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
			if (string.IsNullOrWhiteSpace(styleId))
			{
				return 0;
			}

			var normalized = styleId.Replace(" ", string.Empty);

			// 英文模板是 Heading1..6；中文模板的内置标题样式 id 常直接是 "1".."6"
			if (normalized.StartsWith("Heading", StringComparison.OrdinalIgnoreCase))
			{
				normalized = normalized["Heading".Length..];
			}

			return int.TryParse(normalized, out var level) && level is >= 1 and <= 6 ? level : 0;
		}
	}
}
