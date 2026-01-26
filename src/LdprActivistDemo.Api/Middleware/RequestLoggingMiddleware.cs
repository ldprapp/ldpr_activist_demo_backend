using System.Diagnostics;

namespace LdprActivistDemo.Api.Middleware;

/// <summary>
/// Middleware логирования HTTP-запросов (метод/путь/статус/время) + trace/correlation.
/// </summary>
public sealed class RequestLoggingMiddleware
{
	private readonly RequestDelegate _next;
	private readonly ILogger<RequestLoggingMiddleware> _logger;

	public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
	{
		_next = next;
		_logger = logger;
	}

	public async Task Invoke(HttpContext context)
	{
		if(context.Request.Path.StartsWithSegments("/swagger"))
		{
			await _next(context);
			return;
		}

		var sw = Stopwatch.StartNew();
		try
		{
			await _next(context);
		}
		finally
		{
			sw.Stop();

			var status = context.Response.StatusCode;
			var level = status >= 500 ? LogLevel.Error : status >= 400 ? LogLevel.Warning : LogLevel.Information;

			var traceId = Activity.Current?.Id ?? context.TraceIdentifier;
			var correlationId =
				context.Items.TryGetValue(CorrelationIdMiddleware.ItemKey, out var v) ? v?.ToString() : null;

			_logger.Log(level,
				"HTTP {Method} {Path}{Query} -> {StatusCode} in {ElapsedMs} ms. traceId={TraceId}, correlationId={CorrelationId}.",
				context.Request.Method,
				context.Request.Path,
				context.Request.QueryString,
				status,
				(long)sw.Elapsed.TotalMilliseconds,
				traceId,
				correlationId);
		}
	}
}