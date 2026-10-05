using Volo.Abp.DependencyInjection;

namespace KnowVault.Ingest
{
	/// <summary>按扩展名选择解析器。</summary>
	public class DocumentParserRegistry : ITransientDependency
	{
		private readonly IReadOnlyList<IDocumentParser> _parsers;

		/// <summary>构造函数。</summary>
		/// <param name="parsers">容器里全部解析器实现。</param>
		public DocumentParserRegistry(IEnumerable<IDocumentParser> parsers)
		{
			_parsers = parsers.ToList();
		}

		/// <summary>按扩展名取解析器。</summary>
		/// <param name="extension">含点的小写扩展名。</param>
		/// <returns>匹配的解析器。</returns>
		/// <exception cref="NotSupportedException">没有解析器能处理该扩展名。</exception>
		public IDocumentParser Resolve(string extension)
			=> _parsers.FirstOrDefault(p => p.Extensions.Contains(extension))
			   ?? throw new NotSupportedException($"没有可处理 {extension} 的文档解析器。");
	}
}
