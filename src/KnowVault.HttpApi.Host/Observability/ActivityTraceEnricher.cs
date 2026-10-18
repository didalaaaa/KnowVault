using System.Diagnostics;
using Serilog.Core;
using Serilog.Events;

namespace KnowVault.HttpApi.Host
{
	/// <summary>
	/// 把当前 Activity 的 TraceId/SpanId 写进每条日志，Seq 里日志与 trace 才能互相跳转
	/// </summary>
	public class ActivityTraceEnricher : ILogEventEnricher
	{
		/// <summary>
		/// 为日志事件补充 TraceId 与 SpanId
		/// </summary>
		/// <param name="logEvent">日志事件</param>
		/// <param name="propertyFactory">属性工厂</param>
		public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
		{
			var activity = Activity.Current;
			if (activity is null) return;

			logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("TraceId", activity.TraceId.ToHexString()));
			logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SpanId", activity.SpanId.ToHexString()));
		}
	}
}
