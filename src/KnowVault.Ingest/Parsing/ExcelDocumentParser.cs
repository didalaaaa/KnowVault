using ClosedXML.Excel;
using KnowVault.Domain.Shared;
using Volo.Abp.DependencyInjection;

namespace KnowVault.Ingest
{
	/// <summary>Excel(.xlsx) 解析器：每个工作表一段，行内单元格用 " | " 连接。</summary>
	public class ExcelDocumentParser : IDocumentParser, ITransientDependency
	{
		/// <inheritdoc />
		public DocumentSourceTypeEnum SourceType => DocumentSourceTypeEnum.Excel;

		/// <inheritdoc />
		public IReadOnlyList<string> Extensions { get; } = [".xlsx"];

		/// <inheritdoc />
		public ParsedDocument Parse(Stream content, string fileName)
		{
			using var workbook = new XLWorkbook(content);
			var result = new ParsedDocument { Title = Path.GetFileNameWithoutExtension(fileName) };

			foreach (var worksheet in workbook.Worksheets)
			{
				var rows = worksheet.RowsUsed()
					.Select(row => string.Join(" | ", row.CellsUsed().Select(cell => cell.GetFormattedString().Trim())))
					.Where(line => line.Replace("|", string.Empty).Trim().Length > 0)
					.ToList();

				if (rows.Count == 0) continue;

				result.Sections.Add(new ParsedSection
				{
					HeadingPath = $"工作表 {worksheet.Name}",
					Text = string.Join("\n", rows)
				});
			}

			return result;
		}
	}
}
