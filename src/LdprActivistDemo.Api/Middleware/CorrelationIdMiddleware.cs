using Microsoft.Extensions.Primitives;

namespace LdprActivistDemo.Api.Middleware;

/// <summary>
/// Middleware, который гарантирует наличие correlation-id для каждого HTTP-запроса.
/// </summary>
public sealed class CorrelationIdMiddleware
{
	/// <summary>Имя HTTP-заголовка correlation-id.</summary>
	public const string HeaderName = "X-Correlation-Id";

	/// <summary>Ключ в <see cref="HttpContext.Items"/> для хранения correlation-id.</summary>
	public const string ItemKey = "CorrelationId";

	private readonly RequestDelegate _next;

	public CorrelationIdMiddleware(RequestDelegate next)
	{
		_next = next;
	}

	public async Task Invoke(HttpContext context)
	{
		var correlationId =
			context.Request.Headers.TryGetValue(HeaderName, out var corr) && !StringValues.IsNullOrEmpty(corr)
				? corr.ToString()
				: Guid.NewGuid().ToString("N");

		context.Items[ItemKey] = correlationId;

		context.Response.OnStarting(() =>
		{
			context.Response.Headers[HeaderName] = correlationId;
			return Task.CompletedTask;
		});

		await _next(context);
	}
}