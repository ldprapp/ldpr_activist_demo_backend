using System.Diagnostics;
using System.Text.Json;

using LdprActivistDemo.Contracts.Errors;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;

namespace LdprActivistDemo.Api.Middleware;

public sealed class ActorPasswordHeaderDecodingMiddleware
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	private readonly RequestDelegate _next;

	public ActorPasswordHeaderDecodingMiddleware(RequestDelegate next)
	{
		_next = next;
	}

	public async Task Invoke(HttpContext context)
	{
		if(!context.Request.Headers.TryGetValue(LdprActivistDemo.Api.ActorPasswordHeaders.Base64HeaderName, out var encodedValues)
		   || StringValues.IsNullOrEmpty(encodedValues))
		{
			await _next(context);
			return;
		}

		var encodedValue = encodedValues.ToString().Trim();
		if(encodedValue.Length == 0)
		{
			await _next(context);
			return;
		}

		if(!LdprActivistDemo.Api.ActorPasswordHeaders.TryDecodeBase64(encodedValue, out var decodedPassword)
		   || decodedPassword is null)
		{
			await WriteInvalidHeaderResponseAsync(context);
			return;
		}

		context.Request.Headers[LdprActivistDemo.Api.ActorPasswordHeaders.RawHeaderName] = decodedPassword;

		await _next(context);
	}

	private static async Task WriteInvalidHeaderResponseAsync(HttpContext context)
	{
		context.Response.StatusCode = StatusCodes.Status400BadRequest;
		context.Response.ContentType = "application/problem+json; charset=utf-8";

		var traceId = Activity.Current?.Id ?? context.TraceIdentifier;
		var correlationId = context.Items.TryGetValue(CorrelationIdMiddleware.ItemKey, out var correlationIdValue)
			? correlationIdValue?.ToString()
			: null;

		var problem = new ValidationProblemDetails(new Dictionary<string, string[]>
		{
			["actorUserPassword"] = new[]
			{
				$"ActorUserPassword header is invalid. Use {LdprActivistDemo.Api.ActorPasswordHeaders.Base64HeaderName} with Base64(UTF-8) value."
			},
		})
		{
			Status = StatusCodes.Status400BadRequest,
			Title = "Некорректный запрос.",
			Detail = $"Заголовок {LdprActivistDemo.Api.ActorPasswordHeaders.Base64HeaderName} должен содержать корректное значение Base64(UTF-8).",
			Instance = context.Request.Path.ToString(),
		};

		problem.Extensions["code"] = ApiErrorCodes.ValidationFailed;
		problem.Extensions["traceId"] = traceId;
		if(!string.IsNullOrWhiteSpace(correlationId)) problem.Extensions["correlationId"] = correlationId;

		await context.Response.WriteAsync(JsonSerializer.Serialize(problem, JsonOptions), context.RequestAborted);
	}
}