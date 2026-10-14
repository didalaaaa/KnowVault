namespace KnowVault.HttpApi.Host
{
	/// <summary>
	/// 把 SignalR 握手请求 query string 里的 access_token 提升为 Authorization 头
	/// </summary>
	/// <remarks>
	/// WebSocket 无法携带自定义请求头，SignalR 的 JS 客户端只能把令牌放进 query string；
	/// 而 OpenIddict 的验证处理不读 query string，因此必须在认证中间件之前补一次搬运。
	/// 只对 hub 路径生效，避免把任意接口的令牌暴露面扩大到 URL 上
	/// </remarks>
	public class SignalRAccessTokenMiddleware
	{
		private const string AccessTokenKey = "access_token";

		private readonly RequestDelegate _next;

		/// <summary>
		/// 构造函数
		/// </summary>
		/// <param name="next">下一个中间件</param>
		public SignalRAccessTokenMiddleware(RequestDelegate next) => _next = next;

		/// <summary>
		/// 处理请求：命中 hub 路径且没有 Authorization 头时，用 query string 里的令牌补上
		/// </summary>
		/// <param name="context">HTTP 上下文</param>
		/// <returns>任务</returns>
		public async Task InvokeAsync(HttpContext context)
		{
			var isHubRequest = context.Request.Path.StartsWithSegments(QaHub.Route);
			var hasAuthorization = !string.IsNullOrEmpty(context.Request.Headers.Authorization);
			if (isHubRequest && !hasAuthorization)
			{
				var token = context.Request.Query[AccessTokenKey].ToString();
				if (!string.IsNullOrEmpty(token)) context.Request.Headers.Authorization = $"Bearer {token}";
			}

			await _next(context);
		}
	}
}
