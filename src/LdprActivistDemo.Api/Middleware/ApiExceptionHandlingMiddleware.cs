using System.Diagnostics;
using System.Text.Json;

using LdprActivistDemo.Api.Logging;
using LdprActivistDemo.Application.Diagnostics;
using LdprActivistDemo.Contracts.Errors;

using Microsoft.AspNetCore.Mvc;

namespace LdprActivistDemo.Api.Middleware;

public sealed class ApiExceptionHandlingMiddleware
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	private readonly RequestDelegate _next;
	private readonly ILogger<ApiExceptionHandlingMiddleware> _logger;

	public ApiExceptionHandlingMiddleware(
		RequestDelegate next,
		ILogger<ApiExceptionHandlingMiddleware> logger)
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
			throw;
		}
		catch(Exception ex)
		{
			var traceId = Activity.Current?.Id ?? context.TraceIdentifier;
			var correlationId = context.Items.TryGetValue(CorrelationIdMiddleware.ItemKey, out var value)
				? value?.ToString()
				: null;
			var operation = ApiLogOperations.Http.UnhandledException;

			var commonProperties = new (string Name, object? Value)[]
			{
				("Method", context.Request.Method),
				("Path", context.Request.Path.Value),
				("EndpointDisplayName", context.GetEndpoint()?.DisplayName),
				("TraceId", traceId),
				("TraceIdentifier", context.TraceIdentifier),
				("CorrelationId", correlationId),
			};

			using var scope = _logger.BeginExecutionScope(
				DomainLogEvents.Http.UnhandledException,
				LogLayers.ApiMiddleware,
				operation,
				commonProperties);

			_logger.LogFailed(
				LogLevel.Error,
				DomainLogEvents.Http.UnhandledException,
				LogLayers.ApiMiddleware,
				operation,
				"Unhandled exception while processing HTTP request.",
				ex,
				commonProperties);

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

			await context.Response.WriteAsync(
				JsonSerializer.Serialize(pd, JsonOptions),
				context.RequestAborted);
		}
	}
}