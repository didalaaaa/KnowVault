using KnowVault.Application.Contracts;
using KnowVault.Domain.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace KnowVault.HttpApi
{
	/// <summary>
	/// 流式问答 Hub：把应用层的问答事件流原样推给调用方
	/// </summary>
	/// <remarks>
	/// Hub 方法不能声明 CancellationToken（SignalR 会把它当成客户端参数），
	/// 因此取消令牌取自 <c>Context.ConnectionAborted</c> 并显式传给下游
	/// </remarks>
	[Authorize(KnowVaultPermissions.Qa.Ask)]
	public class QaHub : Hub
	{
		/// <summary>
		/// Hub 路由，供 Host 映射与令牌中间件共用
		/// </summary>
		public const string Route = "/signalr/qa";

		private readonly IQaStreamService _qaStreamService;

		/// <summary>
		/// 构造函数
		/// </summary>
		/// <param name="qaStreamService">流式问答编排服务</param>
		public QaHub(IQaStreamService qaStreamService) => _qaStreamService = qaStreamService;

		/// <summary>
		/// 以服务端流的方式回答问题
		/// </summary>
		/// <param name="input">提问参数（与 REST 同一 DTO）</param>
		/// <returns>问答事件流</returns>
		[HubMethodName("Ask")]
		public IAsyncEnumerable<QaStreamEventDto> AskAsync(AskQuestionInput input) => _qaStreamService.AskStreamAsync(input, Context.ConnectionAborted);
	}
}
