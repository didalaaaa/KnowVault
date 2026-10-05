using System.Text;
using HtmlAgilityPack;
using KnowVault.Domain.Shared;
using Volo.Abp.DependencyInjection;

namespace KnowVault.Ingest
{
	/// <summary>HTML 解析器：剔除脚本/样式/导航，按 h1-h6 + 段落/列表/表格单元格还原结构。</summary>
	public class HtmlDocumentParser : IDocumentParser, ITransientDependency
	{
		/// <inheritdoc />
		public DocumentSourceTypeEnum SourceType => DocumentSourceTypeEnum.Html;

		/// <inheritdoc />
		public IReadOnlyList<string> Extensions { get; } = [".html", ".htm"];

		/// <inheritdoc />
		public ParsedDocument Parse(Stream content, string fileName)
		{
			var html = new HtmlDocument();
			html.Load(content);

			// 脚本/样式/导航栏会污染向量，先整块摘除
			foreach (var node in html.DocumentNode
						 .SelectNodes("//script|//style|//noscript|//nav|//footer")
					 ?? Enumerable.Empty<HtmlNode>())
			{
				node.Remove();
			}

			var result = new ParsedDocument
			{
				Title = html.DocumentNode.SelectSingleNode("//title")?.InnerText.Trim()
						?? Path.GetFileNameWithoutExtension(fileName)
			};

			var headings = new string[7];
			var body = html.DocumentNode.SelectSingleNode("//body") ?? html.DocumentNode;
			foreach (var node in body.DescendantsAndSelf())
			{
				var name = node.Name.ToLowerInvariant();

				if (name.Length == 2 && name[0] == 'h' && name[1] is >= '1' and <= '6')
				{
					var level = name[1] - '0';
					headings[level] = Normalize(node.InnerText);
					for (var deeper = level + 1; deeper <= 6; deeper++)
					{
						headings[deeper] = string.Empty;
					}

					continue;
				}

				if (name is not ("p" or "li" or "td" or "blockquote" or "pre"))
				{
					continue;
				}

				// 嵌套结构会重复取到文本：<li> 里的 <p>、<p> 里的 <span> 都只保留最外层
				var parentName = node.ParentNode?.Name.ToLowerInvariant();
				if (parentName is "li" or "p")
				{
					continue;
				}

				var text = Normalize(node.InnerText);
				if (text.Length > 0)
				{
					result.Sections.Add(new ParsedSection
					{
						HeadingPath = BuildHeadingPath(headings),
						Text = text
					});
				}
			}

			return result;
		}

		private static string BuildHeadingPath(string[] headings)
			=> string.Join(" > ", headings.Skip(1).Where(h => !string.IsNullOrWhiteSpace(h)));

		private static string Normalize(string text)
			=> string.Join(" ", text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
	}
}
