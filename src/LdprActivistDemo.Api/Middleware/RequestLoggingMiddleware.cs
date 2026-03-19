using System.Diagnostics;

using LdprActivistDemo.Api.Logging;
using LdprActivistDemo.Application.Diagnostics;
using LdprActivistDemo.Application.Logging;

namespace LdprActivistDemo.Api.Middleware;

public sealed class RequestLoggingMiddleware
{
	private readonly RequestDelegate _next;
	private readonly ILogger<RequestLoggingMiddleware> _logger;

	public RequestLoggingMiddleware(
		RequestDelegate next,
		ILogger<RequestLoggingMiddleware> logger)
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

		var eventId = DomainLogEvents.Http.Request;
		var operation = ApiLogOperations.Http.Request;

		var commonProperties = new (string Name, object? Value)[]
		{
			("Method", context.Request.Method),
			("Path", context.Request.Path.Value),
			("Query", context.Request.QueryString.Value),
			("ContentType", context.Request.ContentType),
			("EndpointDisplayName", context.GetEndpoint()?.DisplayName),
			("TraceIdentifier", context.TraceIdentifier),
			("CorrelationId", context.Items.TryGetValue(CorrelationIdMiddleware.ItemKey, out var correlationId) ? correlationId : null),
		};

		using var scope = _logger.BeginExecutionScope(
			eventId,
			LogLayers.ApiMiddleware,
			operation,
			commonProperties);

		_logger.LogStarted(
			eventId,
			LogLayers.ApiMiddleware,
			operation,
			"HTTP request started.",
			commonProperties);

		var sw = Stopwatch.StartNew();
		try
		{
			await _next(context);
		}
		catch(OperationCanceledException) when(context.RequestAborted.IsCancellationRequested)
		{
			sw.Stop();

			_logger.LogAborted(
				LogLevel.Information,
				eventId,
				LogLayers.ApiMiddleware,
				operation,
				"HTTP request aborted by client.",
				StructuredLog.Combine(
					commonProperties,
					("ElapsedMs", (long)sw.Elapsed.TotalMilliseconds)));
			throw;
		}
		catch(Exception ex)
		{
			sw.Stop();

			_logger.LogFailed(
				LogLevel.Error,
				eventId,
				LogLayers.ApiMiddleware,
				operation,
				"HTTP request failed with unhandled exception.",
				ex,
				StructuredLog.Combine(
					commonProperties,
					("ElapsedMs", (long)sw.Elapsed.TotalMilliseconds)));

			throw;
		}
		finally
		{
			if(sw.IsRunning)
			{
				sw.Stop();
			}
		}

		var status = context.Response.StatusCode;
		var level = status >= 500
			? LogLevel.Error
			: status >= 400
				? LogLevel.Warning
				: LogLevel.Information;

		if(status >= 400)
		{
			_logger.LogRejected(
				level,
				eventId,
				LogLayers.ApiMiddleware,
				operation,
				"HTTP request completed with non-success status.",
				StructuredLog.Combine(
					commonProperties,
					("StatusCode", status),
					("ElapsedMs", (long)sw.Elapsed.TotalMilliseconds)));

			return;
		}

		_logger.LogCompleted(
			level,
			eventId,
			LogLayers.ApiMiddleware,
			operation,
			"HTTP request completed.",
			StructuredLog.Combine(
				commonProperties,
				("StatusCode", status),
				("ElapsedMs", (long)sw.Elapsed.TotalMilliseconds)));
	}
}