namespace LdprActivistDemo.Api.RateLimiting;

/// <summary>
/// Именованные политики rate limiting для API.
/// </summary>
public static class ApiRateLimitingPolicyNames
{
	public const string PublicAuthentication = "public-authentication";
	public const string PublicOtpConfirmation = "public-otp-confirmation";
	public const string AuthenticatedMutation = "authenticated-mutation";
}

/// <summary>
/// Корневая конфигурация rate limiting для API.
/// </summary>
public sealed class ApiRateLimitingOptions
{
	/// <summary>
	/// Имя конфигурационной секции.
	/// </summary>
	public const string SectionName = "RateLimiting";

	/// <summary>
	/// Включён ли rate limiting.
	/// </summary>
	public bool Enabled { get; set; } = true;

	/// <summary>
	/// Ограничения для публичных auth endpoint-ов.
	/// </summary>
	public ApiFixedWindowRateLimitOptions PublicAuthentication { get; set; } = new()
	{
		PermitLimit = 5,
		WindowSeconds = 60,
		QueueLimit = 0,
	};

	/// <summary>
	/// Ограничения для endpoint-ов подтверждения OTP.
	/// </summary>
	public ApiFixedWindowRateLimitOptions PublicOtpConfirmation { get; set; } = new()
	{
		PermitLimit = 10,
		WindowSeconds = 60,
		QueueLimit = 0,
	};

	/// <summary>
	/// Ограничения для авторизованных mutating endpoint-ов.
	/// </summary>
	public ApiFixedWindowRateLimitOptions AuthenticatedMutation { get; set; } = new()
	{
		PermitLimit = 30,
		WindowSeconds = 60,
		QueueLimit = 0,
	};
}

/// <summary>
/// Общие настройки fixed-window limiter policy.
/// </summary>
public sealed class ApiFixedWindowRateLimitOptions
{
	public int PermitLimit { get; set; } = 10;
	public int WindowSeconds { get; set; } = 60;
	public int QueueLimit { get; set; } = 0;
	public bool AutoReplenishment { get; set; } = true;
}

/// <summary>
/// Построение partition keys для rate limiting.
/// </summary>
public static class ApiRateLimitingPartitionKeys
{
	public static string BuildRemoteIpPartitionKey(HttpContext httpContext, string policyName)
	{
		var remoteIp = httpContext.Connection.RemoteIpAddress?.ToString();
		return string.IsNullOrWhiteSpace(remoteIp)
			? $"{policyName}:ip:unknown"
			: $"{policyName}:ip:{remoteIp}";
	}

	public static string BuildActorOrRemoteIpPartitionKey(HttpContext httpContext, string policyName)
	{
		if(httpContext.Request.Query.TryGetValue("actorUserId", out var rawActorUserId)
		   && Guid.TryParse(rawActorUserId.ToString(), out var actorUserId)
		   && actorUserId != Guid.Empty)
		{
			return $"{policyName}:actor:{actorUserId:N}";
		}

		return BuildRemoteIpPartitionKey(httpContext, policyName);
	}
}