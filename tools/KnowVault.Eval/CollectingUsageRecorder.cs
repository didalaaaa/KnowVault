using KnowVault.Domain;

namespace KnowVault.Eval
{
	/// <summary>
	/// 只收集用量、不写数据库的记录器，用于评估阶段统计 token 与成本
	/// </summary>
	public class CollectingUsageRecorder : IAiUsageRecorder
	{
		private readonly object _sync = new();
		private readonly List<AiUsageEntry> _entries = [];

		/// <inheritdoc />
		public Task RecordAsync(AiUsageEntry entry, CancellationToken cancellationToken = default)
		{
			lock (_sync) _entries.Add(entry);
			return Task.CompletedTask;
		}

		/// <summary>
		/// 清空已收集的记录
		/// </summary>
		public void Reset()
		{
			lock (_sync) _entries.Clear();
		}

		/// <summary>
		/// 取当前记录的副本
		/// </summary>
		/// <returns>用量记录列表</returns>
		public IReadOnlyList<AiUsageEntry> Snapshot()
		{
			lock (_sync) return [.. _entries];
		}
	}
}
