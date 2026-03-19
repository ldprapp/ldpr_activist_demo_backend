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

	public static ActionResult? TryBuildActorRequestValidationProblem(
		this ControllerBase controller,
		Guid actorUserId,
		string? actorUserPassword,
		string actorPasswordHeader = "X-Actor-Password")
	{
		var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
		if(actorUserId == Guid.Empty)
		{
			errors["actorUserId"] = new[] { "ActorUserId is required." };
		}

		if(string.IsNullOrWhiteSpace(actorUserPassword))
		{
			errors["actorUserPassword"] = new[]
			{
				$"ActorUserPassword is required (use {actorPasswordHeader} header)."
			};
		}

		return errors.Count == 0
			? null
			: controller.ValidationProblemWithCode(
				LdprActivistDemo.Contracts.Errors.ApiErrorCodes.ValidationFailed,
				errors,
				title: "Некорректный запрос.",
				detail: $"Передайте actorUserId и заголовок {actorPasswordHeader}.");
	}

	public static ActionResult? TryBuildActorUserMatchValidationProblem(
		this ControllerBase controller,
		Guid actorUserId,
		Guid targetUserId,
		string targetFieldName)
	{
		var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
		if(targetUserId == Guid.Empty)
		{
			errors[targetFieldName] = new[] { $"{targetFieldName} must be non-empty GUID." };
		}
		else if(actorUserId != Guid.Empty && actorUserId != targetUserId)
		{
			errors[targetFieldName] = new[] { $"{targetFieldName} must be equal to actorUserId." };
		}

		return errors.Count == 0
			? null
			: controller.ValidationProblemWithCode(
				LdprActivistDemo.Contracts.Errors.ApiErrorCodes.ValidationFailed,
				errors,
				title: "Некорректный запрос.",
				detail: "Целевой пользователь должен быть явно указан и совпадать с actorUserId.");
	}

	public static ActionResult? TryBuildActorGuidMatchValidationProblem(
		this ControllerBase controller,
		Guid actorUserId,
		Guid comparedGuid,
		string comparedFieldName)
	{
		var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
		if(comparedGuid == Guid.Empty)
		{
			errors[comparedFieldName] = new[] { $"{comparedFieldName} must be non-empty GUID." };
		}
		else if(actorUserId != Guid.Empty && actorUserId != comparedGuid)
		{
			errors[comparedFieldName] = new[] { $"{comparedFieldName} must be equal to actorUserId." };
		}

		return errors.Count == 0
			? null
			: controller.ValidationProblemWithCode(
				LdprActivistDemo.Contracts.Errors.ApiErrorCodes.ValidationFailed,
				errors,
				title: "Некорректный запрос.",
				detail: $"{comparedFieldName} должен быть явно указан и совпадать с actorUserId.");
	}

	public static ActionResult? TryBuildActorPasswordMatchValidationProblem(
		this ControllerBase controller,
		string? actorUserPassword,
		string? comparedPassword,
		string comparedFieldName,
		string actorPasswordHeader = "X-Actor-Password")
	{
		if(string.IsNullOrWhiteSpace(comparedPassword) || string.IsNullOrWhiteSpace(actorUserPassword))
		{
			return null;
		}

		if(string.Equals(actorUserPassword, comparedPassword, StringComparison.Ordinal))
		{
			return null;
		}

		return controller.ValidationProblemWithCode(
			LdprActivistDemo.Contracts.Errors.ApiErrorCodes.ValidationFailed,
			new Dictionary<string, string[]>(StringComparer.Ordinal)
			{
				[comparedPassword == actorUserPassword ? "body" : comparedFieldName] = new[]
				{
					$"{comparedFieldName} must be equal to actorUserPassword from {actorPasswordHeader} header."
				},
			},
			title: "Некорректный запрос.",
			detail: $"{comparedFieldName} должен совпадать с actorUserPassword.");
	}

	private static string? TryGetCorrelationId(HttpContext http)
	{
		if(http.Request.Headers.TryGetValue(CorrelationIdHeader, out var corr) && !StringValues.IsNullOrEmpty(corr))
		{
			return corr.ToString();
		}

		if(http.Items.TryGetValue(CorrelationIdMiddleware.ItemKey, out var value)
		   && value is string s
		   && !string.IsNullOrWhiteSpace(s))
		{
			return s;
		}

		return null;
	}
}