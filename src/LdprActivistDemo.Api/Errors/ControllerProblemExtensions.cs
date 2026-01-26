using System.Diagnostics;

using LdprActivistDemo.Api.Middleware;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;

namespace LdprActivistDemo.Api.Errors;

public static class ControllerProblemExtensions
{
	private const string CorrelationIdHeader = "X-Correlation-Id";

	public static ObjectResult ProblemWithCode(
		this ControllerBase controller,
		int statusCode,
		string code,
		string title,
		string? detail = null)
	{
		var http = controller.HttpContext;

		var pd = new ProblemDetails
		{
			Status = statusCode,
			Title = title,
			Detail = detail,
			Instance = http.Request.Path.ToString(),
		};

		pd.Extensions["code"] = code;
		pd.Extensions["traceId"] = Activity.Current?.Id ?? http.TraceIdentifier;

		var corrId = TryGetCorrelationId(http);
		if(!string.IsNullOrWhiteSpace(corrId))
		{
			pd.Extensions["correlationId"] = corrId;
			http.Response.Headers[CorrelationIdHeader] = corrId;
		}

		return new ObjectResult(pd) { StatusCode = statusCode };
	}

	public static ObjectResult ValidationProblemWithCode(
		this ControllerBase controller,
		string code,
		IDictionary<string, string[]> errors,
		string title = "Некорректный запрос.",
		string? detail = null)
	{
		var http = controller.HttpContext;

		var pd = new ValidationProblemDetails(errors)
		{
			Status = StatusCodes.Status400BadRequest,
			Title = title,
			Detail = detail,
			Instance = http.Request.Path.ToString(),
		};

		pd.Extensions["code"] = code;
		pd.Extensions["traceId"] = Activity.Current?.Id ?? http.TraceIdentifier;

		var corrId = TryGetCorrelationId(http);
		if(!string.IsNullOrWhiteSpace(corrId))
		{
			pd.Extensions["correlationId"] = corrId;
			http.Response.Headers[CorrelationIdHeader] = corrId;
		}

		return new ObjectResult(pd) { StatusCode = StatusCodes.Status400BadRequest };
	}

	private static string? TryGetCorrelationId(HttpContext http)
	{
		if(http.Request.Headers.TryGetValue(CorrelationIdHeader, out var corr) && !StringValues.IsNullOrEmpty(corr))
		{
			return corr.ToString();
		}

		if(http.Items.TryGetValue(CorrelationIdMiddleware.ItemKey, out var v)
			&& v is string s
			&& !string.IsNullOrWhiteSpace(s))
		{
			return s;
		}

		return null;
	}
}