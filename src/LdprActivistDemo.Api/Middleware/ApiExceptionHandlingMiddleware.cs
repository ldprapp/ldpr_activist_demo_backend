using System.Diagnostics;
using System.Text.Json;

using LdprActivistDemo.Contracts.Errors;

using Microsoft.AspNetCore.Mvc;

namespace LdprActivistDemo.Api.Middleware;

/// <summary>
/// Middleware, который превращает необработанные исключения в ProblemDetails и логирует их.
/// </summary>
public sealed class ApiExceptionHandlingMiddleware
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	private readonly RequestDelegate _next;
	private readonly ILogger<ApiExceptionHandlingMiddleware> _logger;

	public ApiExceptionHandlingMiddleware(RequestDelegate next, ILogger<ApiExceptionHandlingMiddleware> logger)
	{
		_next = next;
		_logger = logger;
	}

	public async Task Invoke(HttpContext context)
	{
		try
		{
			await _next(context);
		}
		catch(OperationCanceledException) when(context.RequestAborted.IsCancellationRequested)
		{
			_logger.LogInformation("Request aborted by client: {Method} {Path}.",
				context.Request.Method,
				context.Request.Path);
		}
		catch(Exception ex)
		{
			var traceId = Activity.Current?.Id ?? context.TraceIdentifier;
			var correlationId =
				context.Items.TryGetValue(CorrelationIdMiddleware.ItemKey, out var v) ? v?.ToString() : null;

			_logger.LogError(ex,
				"Unhandled exception for HTTP {Method} {Path}. traceId={TraceId}, correlationId={CorrelationId}.",
				context.Request.Method,
				context.Request.Path,
				traceId,
				correlationId);

			if(context.Response.HasStarted)
			{
				throw;
			}

			context.Response.Clear();
			context.Response.StatusCode = StatusCodes.Status500InternalServerError;
			context.Response.ContentType = "application/problem+json; charset=utf-8";

			var pd = new ProblemDetails
			{
				Status = StatusCodes.Status500InternalServerError,
				Title = "Внутренняя ошибка.",
				Detail = "Произошла непредвиденная ошибка на сервере.",
				Instance = context.Request.Path.ToString(),
			};

			pd.Extensions["code"] = ApiErrorCodes.InternalError;
			pd.Extensions["traceId"] = traceId;
			if(!string.IsNullOrWhiteSpace(correlationId))
			{
				pd.Extensions["correlationId"] = correlationId;
			}

			await context.Response.WriteAsync(JsonSerializer.Serialize(pd, JsonOptions), context.RequestAborted);
		}
	}
}