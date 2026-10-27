namespace KnowVault.Eval
{
	/// <summary>
	/// 评估工具的命令行参数
	/// </summary>
	public class EvalOptions
	{
		/// <summary>
		/// 是否只打印帮助
		/// </summary>
		public bool ShowHelp { get; init; }

		/// <summary>
		/// 评估使用的知识库 ID
		/// </summary>
		public Guid KnowledgeBaseId { get; init; }

		/// <summary>
		/// 租户 ID；单租户运行为空 GUID
		/// </summary>
		public Guid TenantId { get; init; } = Guid.Empty;

		/// <summary>
		/// 金标集路径
		/// </summary>
		public string GoldenSetPath { get; init; } = string.Empty;

		/// <summary>
		/// 语料目录
		/// </summary>
		public string CorpusDirectory { get; init; } = string.Empty;

		/// <summary>
		/// 参与评估的模式
		/// </summary>
		public IReadOnlyList<EvalModeEnum> Modes { get; init; } = [];

		/// <summary>
		/// 最终引用条数
		/// </summary>
		public int TopK { get; init; } = 5;

		/// <summary>
		/// 题目之间的等待毫秒数
		/// </summary>
		public int DelayMs { get; init; } = 500;

		/// <summary>
		/// 是否只跑检索，不调用生成模型
		/// </summary>
		public bool RetrievalOnly { get; init; }

		/// <summary>
		/// 只跑前 N 道题，便于调试
		/// </summary>
		public int? QuestionLimit { get; init; }

		/// <summary>
		/// Markdown 报告输出路径
		/// </summary>
		public string OutputPath { get; init; } = string.Empty;

		/// <summary>
		/// 应用配置文件路径
		/// </summary>
		public string ConfigPath { get; init; } = string.Empty;

		/// <summary>
		/// 仓库根目录
		/// </summary>
		public string RepositoryRoot { get; init; } = string.Empty;

		/// <summary>
		/// 解析命令行参数
		/// </summary>
		/// <param name="args">命令行参数</param>
		/// <param name="repositoryRoot">仓库根目录</param>
		/// <returns>解析后的选项</returns>
		/// <exception cref="ArgumentException">参数缺失或取值非法</exception>
		public static EvalOptions Parse(string[] args, string repositoryRoot)
		{
			if (args.Length == 0)
			{
				return new EvalOptions { ShowHelp = true, RepositoryRoot = repositoryRoot };
			}

			var showHelp = false;
			Guid? knowledgeBaseId = null;
			var goldenSet = Path.Combine(repositoryRoot, "docs", "eval", "golden-set.json");
			var corpusDirectory = Path.Combine(repositoryRoot, "docs", "eval", "corpus");
			var configPath = Path.Combine(repositoryRoot, "src", "KnowVault.HttpApi.Host", "appsettings.json");
			var output = Path.Combine(repositoryRoot, "docs", "eval", "reports", $"{DateTime.Today:yyyy-MM-dd}-p5-eval.md");
			var modes = new List<EvalModeEnum> { EvalModeEnum.Vector, EvalModeEnum.Hybrid, EvalModeEnum.HybridRewrite };
			var topK = 5;
			var delayMs = 500;
			var retrievalOnly = false;
			int? questionLimit = null;

			for (var index = 0; index < args.Length; index++)
			{
				var argument = args[index];

				string NextValue()
				{
					if (++index >= args.Length) throw new ArgumentException($"参数 {argument} 缺少取值");
					return args[index];
				}

				switch (argument)
				{
					case "--help":
					case "-h":
						showHelp = true;
						break;
					case "--knowledge-base-id":
						knowledgeBaseId = Guid.TryParse(NextValue(), out var parsed) ? parsed : throw new ArgumentException("--knowledge-base-id 必须是合法 GUID");
						break;
					case "--golden-set":
						goldenSet = ResolvePath(NextValue(), repositoryRoot);
						break;
					case "--corpus-dir":
						corpusDirectory = ResolvePath(NextValue(), repositoryRoot);
						break;
					case "--config":
						configPath = ResolvePath(NextValue(), repositoryRoot);
						break;
					case "--modes":
						modes = [.. NextValue()
							.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
							.Select(ParseMode)
							.Distinct()];
						if (modes.Count == 0) throw new ArgumentException("--modes 至少需要一个模式");
						break;
					case "--top-k":
						topK = ParseInt(NextValue(), "--top-k");
						if (topK is < 1 or > 8) throw new ArgumentException("--top-k 必须在 1 到 8 之间");
						break;
					case "--delay-ms":
						delayMs = ParseInt(NextValue(), "--delay-ms");
						if (delayMs < 0) throw new ArgumentException("--delay-ms 不能为负数");
						break;
					case "--retrieval-only":
						retrievalOnly = true;
						break;
					case "--question-limit":
						questionLimit = ParseInt(NextValue(), "--question-limit");
						if (questionLimit <= 0) throw new ArgumentException("--question-limit 必须大于 0");
						break;
					case "--output":
						output = ResolvePath(NextValue(), repositoryRoot);
						break;
					default:
						throw new ArgumentException($"未知参数：{argument}");
				}
			}

			if (showHelp) return new EvalOptions { ShowHelp = true, RepositoryRoot = repositoryRoot };
			if (!knowledgeBaseId.HasValue) throw new ArgumentException("缺少必填参数 --knowledge-base-id");

			return new EvalOptions
			{
				KnowledgeBaseId = knowledgeBaseId.Value,
				GoldenSetPath = goldenSet,
				CorpusDirectory = corpusDirectory,
				ConfigPath = configPath,
				OutputPath = output,
				Modes = modes,
				TopK = topK,
				DelayMs = delayMs,
				RetrievalOnly = retrievalOnly,
				QuestionLimit = questionLimit,
				RepositoryRoot = repositoryRoot
			};
		}

		/// <summary>
		/// 打印命令行用法
		/// </summary>
		public static void PrintUsage()
		{
			Console.WriteLine("KnowVault P5 检索评估工具");
			Console.WriteLine();
			Console.WriteLine("用法：");
			Console.WriteLine("  dotnet run --project tools/KnowVault.Eval -- --knowledge-base-id <GUID> [选项]");
			Console.WriteLine();
			Console.WriteLine("选项：");
			Console.WriteLine("  --golden-set <路径>       金标集，默认 docs/eval/golden-set.json");
			Console.WriteLine("  --corpus-dir <路径>       语料目录，默认 docs/eval/corpus");
			Console.WriteLine("  --config <路径>           应用配置，默认 src/KnowVault.HttpApi.Host/appsettings.json");
			Console.WriteLine("  --modes <列表>            vector,hybrid,hybrid-rewrite，默认全部");
			Console.WriteLine("  --top-k <1-8>             最终引用条数，默认 5");
			Console.WriteLine("  --delay-ms <毫秒>         题目间隔，默认 500");
			Console.WriteLine("  --retrieval-only          只跑检索，不调用 DeepSeek");
			Console.WriteLine("  --question-limit <N>      只跑前 N 道题");
			Console.WriteLine("  --output <路径>           Markdown 报告，默认 docs/eval/reports/<日期>-p5-eval.md");
			Console.WriteLine();
			Console.WriteLine("API Key 只从环境变量读取：Ai__SiliconFlowApiKey、Ai__DeepSeekApiKey");
		}

		/// <summary>
		/// 从当前目录向上查找仓库根目录
		/// </summary>
		/// <returns>包含 KnowVault.slnx 的目录</returns>
		/// <exception cref="InvalidOperationException">找不到仓库根目录</exception>
		public static string FindRepositoryRoot()
		{
			foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
			{
				var directory = new DirectoryInfo(start);
				while (directory is not null)
				{
					if (File.Exists(Path.Combine(directory.FullName, "KnowVault.slnx"))) return directory.FullName;
					directory = directory.Parent;
				}
			}

			throw new InvalidOperationException("找不到仓库根目录（KnowVault.slnx），请在 KnowVault 仓库内运行");
		}

		private static EvalModeEnum ParseMode(string value) => value.ToLowerInvariant() switch
		{
			"vector" => EvalModeEnum.Vector,
			"hybrid" => EvalModeEnum.Hybrid,
			"hybrid-rewrite" => EvalModeEnum.HybridRewrite,
			_ => throw new ArgumentException($"未知模式：{value}（可选 vector / hybrid / hybrid-rewrite）")
		};

		private static int ParseInt(string value, string name)
			=> int.TryParse(value, out var number) ? number : throw new ArgumentException($"{name} 必须是整数");

		private static string ResolvePath(string path, string repositoryRoot)
			=> Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(repositoryRoot, path));
	}
}
