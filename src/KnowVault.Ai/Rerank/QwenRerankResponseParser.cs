using System.Text.Json;

namespace KnowVault.Ai
{
	/// <summary>
	/// DashScope 文本重排接口响应的解析（纯函数，便于单测）
	/// </summary>
	public static class QwenRerankResponseParser
	{
		/// <summary>
		/// 解析重排响应并按 relevance_score 降序返回
		/// </summary>
		/// <param name="json">接口返回的 JSON 文本</param>
		/// <returns>重排结果；缺少必要字段的条目会被跳过</returns>
		/// <exception cref="JsonException">响应不是合法 JSON</exception>
		public static IReadOnlyList<RerankResult> Parse(string json)
		{
			using var document = JsonDocument.Parse(json);
			var results = new List<RerankResult>();

			if (!document.RootElement.TryGetProperty("output", out var output)) return results;
			if (!output.TryGetProperty("results", out var items) || items.ValueKind != JsonValueKind.Array) return results;

			foreach (var item in items.EnumerateArray())
			{
				if (!item.TryGetProperty("index", out var indexElement) || !indexElement.TryGetInt32(out var index)) continue;
				if (!item.TryGetProperty("relevance_score", out var scoreElement) || !scoreElement.TryGetDouble(out var score)) continue;

				results.Add(new RerankResult { Index = index, Score = score });
			}

			// 服务端当前就按分数降序返回，但排序是我们的契约，不能依赖对端行为
			return [.. results.OrderByDescending(result => result.Score)];
		}
	}
}
