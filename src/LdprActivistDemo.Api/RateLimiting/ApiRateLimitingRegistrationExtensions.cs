using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Threading.RateLimiting;

using LdprActivistDemo.Api.Middleware;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LdprActivistDemo.Api.RateLimiting;

/// <summary>
/// Регистрация rate limiting для API.
/// </summary>
public static class ApiRateLimitingRegistrationExtensions
{
	private const string RateLimitExceededCode = "rate_limit_exceeded";
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	/// <summary>
	/// Регистрирует rate limiting policies для API.
	/// </summary>
	public static IServiceCollection AddApiRateLimiting(
		this IServiceCollection services,
		ApiRateLimitingOptions options)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(options);

		if(!options.Enabled)
		{
			return services;
		}

		services.AddRateLimiter(rateLimiterOptions =>
		{
			rateLimiterOptions.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
			rateLimiterOptions.OnRejected = WriteRejectedResponseAsync;

			AddFixedWindowPolicy(
				rateLimiterOptions,
				ApiRateLimitingPolicyNames.PublicAuthentication,
				options.PublicAuthentication,
				httpContext => ApiRateLimitingPartitionKeys.BuildRemoteIpPartitionKey(
					httpContext,
					ApiRateLimitingPolicyNames.PublicAuthentication));

			AddFixedWindowPolicy(
				rateLimiterOptions,
				ApiRateLimitingPolicyNames.PublicOtpConfirmation,
				options.PublicOtpConfirmation,
				httpContext => ApiRateLimitingPartitionKeys.BuildRemoteIpPartitionKey(
					httpContext,
					ApiRateLimitingPolicyNames.PublicOtpConfirmation));

			AddFixedWindowPolicy(
				rateLimiterOptions,
				ApiRateLimitingPolicyNames.AuthenticatedMutation,
				options.AuthenticatedMutation,
				httpContext => ApiRateLimitingPartitionKeys.BuildActorOrRemoteIpPartitionKey(
					httpContext,
					ApiRateLimitingPolicyNames.AuthenticatedMutation));
		});

		return services;
	}

	private static void AddFixedWindowPolicy(
		RateLimiterOptions rateLimiterOptions,
		string policyName,
		ApiFixedWindowRateLimitOptions options,
		Func<HttpContext, string> partitionKeyFactory)
	{
		rateLimiterOptions.AddPolicy<string>(
			policyName,
			httpContext => RateLimitPartition.GetFixedWindowLimiter(
				partitionKey: partitionKeyFactory(httpContext),
				factory: _ => new FixedWindowRateLimiterOptions
				{
					PermitLimit = Math.Max(1, options.PermitLimit),
					Window = TimeSpan.FromSeconds(Math.Max(1, options.WindowSeconds)),
					QueueLimit = Math.Max(0, options.QueueLimit),
					QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
					AutoReplenishment = options.AutoReplenishment,
				}));
	}

	private static async ValueTask WriteRejectedResponseAsync(
		OnRejectedContext context,
		CancellationToken cancellationToken)
	{
		var httpContext = context.HttpContext;
		var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;
		var correlationId = TryGetCorrelationId(httpContext);

		int? retryAfterSeconds = null;
		if(context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
		{
			retryAfterSeconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
			httpContext.Response.Headers.RetryAfter = retryAfterSeconds.Value.ToString(CultureInfo.InvariantCulture);
		}

		if(!string.IsNullOrWhiteSpace(correlationId))
		{
			httpContext.Response.Headers[CorrelationIdMiddleware.HeaderName] = correlationId;
		}

		httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
		httpContext.Response.ContentType = "application/problem+json; charset=utf-8";

		var problem = new ProblemDetails
		{
			Status = StatusCodes.Status429TooManyRequests,
			Title = "Слишком много запросов.",
			Detail = "Лимит запросов для этого endpoint временно исчерпан. Повторите попытку позже.",
			Instance = httpContext.Request.Path.ToString(),
		};

		problem.Extensions["code"] = RateLimitExceededCode;
		problem.Extensions["traceId"] = traceId;

		if(!string.IsNullOrWhiteSpace(correlationId))
		{
			problem.Extensions["correlationId"] = correlationId;
		}

		if(retryAfterSeconds.HasValue)
		{
			problem.Extensions["retryAfterSeconds"] = retryAfterSeconds.Value;
		}

		await httpContext.Response.WriteAsync(
			JsonSerializer.Serialize(problem, JsonOptions),
			cancellationToken);
	}

	private static string? TryGetCorrelationId(HttpContext httpContext)
	{
		if(httpContext.Items.TryGetValue(CorrelationIdMiddleware.ItemKey, out var correlationIdValue)
		   && correlationIdValue is string correlationId
		   && !string.IsNullOrWhiteSpace(correlationId))
		{
			return correlationId;
		}

		if(httpContext.Request.Headers.TryGetValue(CorrelationIdMiddleware.HeaderName, out var rawCorrelationId))
		{
			var headerCorrelationId = rawCorrelationId.ToString();
			if(!string.IsNullOrWhiteSpace(headerCorrelationId))
			{
				return headerCorrelationId;
			}
		}

		return null;
	}
}