using System.Text;
using ClosedXML.Excel;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using KnowVault.Domain.Shared;
using KnowVault.Ingest;
using Xunit;

namespace KnowVault.Tests
{
	/// <summary>文档解析器的纯函数测试（不依赖网络与数据库）。</summary>
	public class DocumentParserTests
	{
		/// <summary>Markdown 的标题层级要拼成 HeadingPath，段落按顺序产出。</summary>
		[Fact]
		public void Markdown_parser_builds_heading_path()
		{
			const string markdown = "# 第一章\n\n第一段内容。\n\n## 1.1 小节\n\n第二段内容。\n";
			var parser = new MarkdownDocumentParser();

			var parsed = parser.Parse(new MemoryStream(Encoding.UTF8.GetBytes(markdown)), "架构.md");

			Assert.Equal(2, parsed.Sections.Count);
			Assert.Equal("第一章", parsed.Sections[0].HeadingPath);
			Assert.Equal("第一段内容。", parsed.Sections[0].Text);
			Assert.Equal("第一章 > 1.1 小节", parsed.Sections[1].HeadingPath);
		}

		/// <summary>列表项不能重复产出（ListBlock 与其内部 ParagraphBlock 都会命中前序遍历）。</summary>
		[Fact]
		public void Markdown_parser_does_not_duplicate_list_items()
		{
			const string markdown = "# 清单\n\n- 条目一\n- 条目二\n";
			var parser = new MarkdownDocumentParser();

			var parsed = parser.Parse(new MemoryStream(Encoding.UTF8.GetBytes(markdown)), "清单.md");

			Assert.Equal(2, parsed.Sections.Count);
			Assert.Single(parsed.Sections, s => s.Text.Contains("条目一"));
		}

		/// <summary>HTML 要剔除脚本与样式内容，只留正文文本。</summary>
		[Fact]
		public void Html_parser_skips_script_and_style()
		{
			const string html = """
				<html><head><title>测试页</title><style>body{color:red}</style></head>
				<body><h1>标题一</h1><p>正文一段。</p><script>var x=1;</script><ul><li>条目一</li></ul></body></html>
				""";
			var parser = new HtmlDocumentParser();

			var parsed = parser.Parse(new MemoryStream(Encoding.UTF8.GetBytes(html)), "page.html");

			Assert.Equal("测试页", parsed.Title);
			Assert.Contains(parsed.Sections, s => s.Text.Contains("正文一段。"));
			Assert.Contains(parsed.Sections, s => s.Text.Contains("条目一"));
			Assert.DoesNotContain(parsed.Sections, s => s.Text.Contains("var x=1"));
			Assert.DoesNotContain(parsed.Sections, s => s.Text.Contains("color:red"));
		}

		/// <summary>Word：Heading1 样式要进 HeadingPath，正文段落要产出。</summary>
		[Fact]
		public void Word_parser_reads_heading_style()
		{
			using var buffer = new MemoryStream();
			using (var document = WordprocessingDocument.Create(buffer, WordprocessingDocumentType.Document))
			{
				var main = document.AddMainDocumentPart();
				main.Document = new Document(new Body(
					new Paragraph(new ParagraphProperties(new ParagraphStyleId { Val = "Heading1" }), new Run(new Text("第一章"))),
					new Paragraph(new Run(new Text("正文内容")))));
				main.Document.Save();
			}

			buffer.Position = 0;
			var parsed = new WordDocumentParser().Parse(buffer, "设计.docx");

			Assert.Single(parsed.Sections);
			Assert.Equal("第一章", parsed.Sections[0].HeadingPath);
			Assert.Equal("正文内容", parsed.Sections[0].Text);
		}

		/// <summary>Excel：每个工作表产出一段，行内用 " | " 连接。</summary>
		[Fact]
		public void Excel_parser_joins_cells_with_pipe()
		{
			using var buffer = new MemoryStream();
			using (var workbook = new XLWorkbook())
			{
				var sheet = workbook.AddWorksheet("人员");
				sheet.Cell("A1").Value = "姓名";
				sheet.Cell("B1").Value = "部门";
				sheet.Cell("A2").Value = "张三";
				sheet.Cell("B2").Value = "研发";
				workbook.SaveAs(buffer);
			}

			buffer.Position = 0;
			var parsed = new ExcelDocumentParser().Parse(buffer, "名单.xlsx");

			Assert.Single(parsed.Sections);
			Assert.Equal("工作表 人员", parsed.Sections[0].HeadingPath);
			Assert.Contains("姓名 | 部门", parsed.Sections[0].Text);
			Assert.Contains("张三 | 研发", parsed.Sections[0].Text);
		}

		/// <summary>未知扩展名必须抛错，而不是静默返回空文档。</summary>
		[Fact]
		public void Registry_throws_for_unknown_extension()
		{
			var registry = new DocumentParserRegistry([new MarkdownDocumentParser()]);
			Assert.Throws<NotSupportedException>(() => registry.Resolve(".txt"));
		}
	}
}
