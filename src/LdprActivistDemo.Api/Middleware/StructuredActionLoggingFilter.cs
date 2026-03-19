using LdprActivistDemo.Application.Diagnostics;

using Microsoft.AspNetCore.Mvc.Filters;

namespace LdprActivistDemo.Api.Middleware;

public sealed class StructuredActionLoggingFilter : IAsyncActionFilter
{
	private readonly ILogger<StructuredActionLoggingFilter> _logger;

	public StructuredActionLoggingFilter(ILogger<StructuredActionLoggingFilter> logger)
	{
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}
	public async Task OnActionExecutionAsync(
		ActionExecutingContext context,
		ActionExecutionDelegate next)
	{
		var http = context.HttpContext;
		if(http.Request.Path.StartsWithSegments("/swagger"))
		{
			await next();
			return;
		}

		var controller = context.ActionDescriptor.RouteValues.TryGetValue("controller", out var controllerName)
			? controllerName
			: null;
		var action = context.ActionDescriptor.RouteValues.TryGetValue("action", out var actionName)
			? actionName
			: null;

		var commonProperties = new (string Name, object? Value)[]
{
			("Controller", controller),
			("Action", action),
			("Method", http.Request.Method),
			("Path", http.Request.Path.Value),
};

		var eventName = DomainLogEvents.Api.Action;

		using var scope = _logger.BeginExecutionScope(
			eventName,
			LogLayers.ApiController,
			"ControllerAction",
			commonProperties);

		_logger.LogStarted(
			eventName,
			LogLayers.ApiController,
			"ControllerAction",
			"Controller action execution started.",
			commonProperties);

		var executedContext = await next();

		if(executedContext.Exception is not null && !executedContext.ExceptionHandled)
		{
			_logger.LogFailed(
				LogLevel.Error,
				eventName,
				LogLayers.ApiController,
				"ControllerAction",
				"Controller action execution failed with unhandled exception.",
				executedContext.Exception,
				commonProperties);
			return;
		}

		var statusCode = http.Response.StatusCode;
		var level = statusCode >= 500
			? LogLevel.Error
			: statusCode >= 400
				? LogLevel.Warning
				: LogLevel.Information;

		if(statusCode >= 400)
		{
			_logger.LogRejected(
				level,
				eventName,
				LogLayers.ApiController,
				"ControllerAction",
				"Controller action execution completed with non-success status.",
				LdprActivistDemo.Application.Logging.StructuredLog.Combine(
					commonProperties,
					("StatusCode", statusCode)));

			return;
		}

		_logger.LogCompleted(
			level,
			eventName,
			LogLayers.ApiController,
			"ControllerAction",
			"Controller action execution completed.",
			LdprActivistDemo.Application.Logging.StructuredLog.Combine(
				commonProperties,
				("StatusCode", statusCode)));
	}
}