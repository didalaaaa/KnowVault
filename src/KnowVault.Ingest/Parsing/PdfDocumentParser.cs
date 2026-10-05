using KnowVault.Domain.Shared;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using Volo.Abp.DependencyInjection;

namespace KnowVault.Ingest
{
	/// <summary>PDF 解析器：按页抽文本（P1 不还原标题层级，用页码充当 HeadingPath）。</summary>
	public class PdfDocumentParser : IDocumentParser, ITransientDependency
	{
		/// <inheritdoc />
		public DocumentSourceTypeEnum SourceType => DocumentSourceTypeEnum.Pdf;

		/// <inheritdoc />
		public IReadOnlyList<string> Extensions { get; } = [".pdf"];

		/// <inheritdoc />
		public ParsedDocument Parse(Stream content, string fileName)
		{
			using var document = PdfDocument.Open(content);
			var result = new ParsedDocument { Title = Path.GetFileNameWithoutExtension(fileName) };

			foreach (var page in document.GetPages())
			{
				// ContentOrderTextExtractor 按阅读顺序拼文本，比直接取 page.Text 的碎片更连贯
				var text = ContentOrderTextExtractor.GetText(page);
				if (string.IsNullOrWhiteSpace(text))
				{
					continue;
				}

				result.Sections.Add(new ParsedSection
				{
					HeadingPath = $"第 {page.Number} 页",
					Text = text.Trim()
				});
			}

			return result;
		}
	}
}
