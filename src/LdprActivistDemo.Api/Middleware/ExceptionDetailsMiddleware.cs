using LdprActivistDemo.Contracts.Errors;

using Microsoft.AspNetCore.Mvc;

namespace LdprActivistDemo.Api.Middleware;

public sealed class ExceptionDetailsMiddleware
{
	private readonly RequestDelegate _next;
	private readonly ILogger<ExceptionDetailsMiddleware> _logger;
	private readonly bool _includeExceptionDetails;

	public ExceptionDetailsMiddleware(
		RequestDelegate next,
		ILogger<ExceptionDetailsMiddleware> logger,
		IHostEnvironment environment,
		IConfiguration configuration)
	{
		_next = next;
		_logger = logger;

		_includeExceptionDetails =
			environment.IsDevelopment()
			|| configuration.GetValue<bool>("Debug:IncludeExceptionDetails");
	}

	public async Task Invoke(HttpContext context)
	{
		try
		{
			await _next(context);
		}
		catch(Exception ex)
		{
			_logger.LogError(
				ex,
				"Unhandled exception. Method={Method}, Path={Path}, TraceId={TraceId}.",
				context.Request.Method,
				context.Request.Path.Value ?? string.Empty,
				context.TraceIdentifier);

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
				Detail = _includeExceptionDetails ? ex.ToString() : "Внутренняя ошибка.",
			};

			pd.Extensions["code"] = ApiErrorCodes.InternalError;
			pd.Extensions["traceId"] = context.TraceIdentifier;

			await context.Response.WriteAsJsonAsync(pd);
		}
	}
}