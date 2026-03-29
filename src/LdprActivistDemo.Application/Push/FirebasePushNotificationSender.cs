using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

using LdprActivistDemo.Application.Diagnostics;
using LdprActivistDemo.Application.Logging;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LdprActivistDemo.Application.Push;

public sealed class FirebasePushNotificationSender : IPushNotificationSender
{
	private const string EventName = "push.sender.send";
	private const string OperationName = "push.sender.firebase.send";
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	private readonly System.Net.Http.HttpClient _httpClient;
	private readonly IConfiguration _configuration;
	private readonly IFirebaseAccessTokenProvider _accessTokenProvider;
	private readonly ILogger<FirebasePushNotificationSender> _logger;

	public FirebasePushNotificationSender(
		System.Net.Http.HttpClient httpClient,
		IConfiguration configuration,
		IFirebaseAccessTokenProvider accessTokenProvider,
		ILogger<FirebasePushNotificationSender> logger)
	{
		_httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
		_configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
		_accessTokenProvider = accessTokenProvider ?? throw new ArgumentNullException(nameof(accessTokenProvider));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	public async Task<PushSendResult> SendManyAsync(
		IReadOnlyList<PushTargetModel> targets,
		PushMessage message,
		CancellationToken cancellationToken)
	{
		var normalizedTargets = NormalizeTargets(targets);
		var settings = FirebasePushSettings.Load(_configuration);
		var properties = new (string Name, object? Value)[]
		{
			("TargetCount", normalizedTargets.Count),
			("Title", message.Title),
			("Enabled", settings.Enabled),
		};

		using var scope = _logger.BeginExecutionScope(
			EventName,
			LogLayers.ApplicationService,
			OperationName,
			properties);

		_logger.LogStarted(
			EventName,
			LogLayers.ApplicationService,
			OperationName,
			"Firebase push sender started.",
			properties);

		try
		{
			if(normalizedTargets.Count == 0)
			{
				_logger.LogCompleted(
					LogLevel.Debug,
					EventName,
					LogLayers.ApplicationService,
					OperationName,
					"Firebase push sender completed. No targets to send.",
					properties);

				return PushSendResult.Empty;
			}

			if(!settings.Enabled)
			{
				_logger.LogRejected(
					LogLevel.Warning,
					EventName,
					LogLayers.ApplicationService,
					OperationName,
					"Firebase push sender rejected sending because it is disabled in configuration.",
					properties);

				return new PushSendResult(
					SuccessCount: 0,
					FailureCount: normalizedTargets.Count,
					InvalidTokens: Array.Empty<string>());
			}

			var projectId = NormalizeOptional(settings.ProjectId);
			if(projectId is null)
			{
				_logger.LogRejected(
					LogLevel.Warning,
					EventName,
					LogLayers.ApplicationService,
					OperationName,
					"Firebase push sender rejected sending because ProjectId is not configured.",
					properties);

				return new PushSendResult(
					SuccessCount: 0,
					FailureCount: normalizedTargets.Count,
					InvalidTokens: Array.Empty<string>());
			}

			string accessToken;
			try
			{
				accessToken = await _accessTokenProvider.GetAccessTokenAsync(cancellationToken);
			}
			catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
			{
				throw;
			}
			catch(Exception ex)
			{
				_logger.LogFailed(
					LogLevel.Error,
					EventName,
					LogLayers.ApplicationService,
					OperationName,
					"Firebase push sender failed before dispatch because access token acquisition failed.",
					ex,
					properties);

				return new PushSendResult(
					SuccessCount: 0,
					FailureCount: normalizedTargets.Count,
					InvalidTokens: Array.Empty<string>());
			}

			var successCount = 0;
			var failureCount = 0;
			var invalidTokens = new HashSet<string>(StringComparer.Ordinal);
			var sendUri = BuildSendUri(settings.BaseUrl, projectId);

			for(var i = 0; i < normalizedTargets.Count; i++)
			{
				cancellationToken.ThrowIfCancellationRequested();

				var target = normalizedTargets[i];
				try
				{
					var sendResult = await SendSingleAsync(
						sendUri,
						accessToken,
						target,
						message,
						cancellationToken);

					if(sendResult.IsSuccess)
					{
						successCount++;
					}
					else
					{
						failureCount++;
						if(sendResult.IsInvalidToken)
						{
							invalidTokens.Add(target.Token);
						}
					}
				}
				catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
				{
					throw;
				}
				catch(Exception ex)
				{
					failureCount++;
					_logger.LogFailed(
						LogLevel.Error,
						EventName,
						LogLayers.ApplicationService,
						OperationName,
						"Firebase push sender failed while sending a message to a target.",
						ex,
						("TargetUserId", target.UserId),
						("Token", MaskToken(target.Token)));
				}
			}

			var result = new PushSendResult(
				SuccessCount: successCount,
				FailureCount: failureCount,
				InvalidTokens: invalidTokens.ToArray());

			_logger.LogCompleted(
				LogLevel.Information,
				EventName,
				LogLayers.ApplicationService,
				OperationName,
				"Firebase push sender completed.",
				StructuredLog.Combine(
					properties,
					("SuccessCount", result.SuccessCount),
					("FailureCount", result.FailureCount),
					("InvalidTokenCount", result.InvalidTokens.Count)));

			return result;
		}
		catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
		{
			_logger.LogAborted(
				LogLevel.Information,
				EventName,
				LogLayers.ApplicationService,
				OperationName,
				"Firebase push sender aborted.",
				properties);
			throw;
		}
	}

	private async Task<SingleSendResult> SendSingleAsync(
		Uri sendUri,
		string accessToken,
		PushTargetModel target,
		PushMessage message,
		CancellationToken cancellationToken)
	{
		var payload = new FirebaseSendRequest(
			new FirebaseMessageRequest(
				target.Token,
				new FirebaseNotificationRequest(message.Title, message.Body),
				message.Data is { Count: > 0 }
					? message.Data.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal)
					: null));

		var json = JsonSerializer.Serialize(payload, JsonOptions);

		using var request = new HttpRequestMessage(HttpMethod.Post, sendUri);
		request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
		request.Content = new StringContent(json, Encoding.UTF8, "application/json");

		using var response = await _httpClient.SendAsync(request, cancellationToken);
		var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

		if(response.IsSuccessStatusCode)
		{
			return SingleSendResult.Success();
		}

		var invalidToken = IsInvalidTokenResponse(response.StatusCode, responseBody);

		_logger.LogRejected(
			LogLevel.Warning,
			EventName,
			LogLayers.ApplicationService,
			OperationName,
			"Firebase provider rejected push message.",
			("TargetUserId", target.UserId),
			("Token", MaskToken(target.Token)),
			("StatusCode", (int)response.StatusCode),
			("InvalidToken", invalidToken),
			("ResponseBody", TrimResponseBody(responseBody)));

		return SingleSendResult.Fail(invalidToken);
	}

	private static Uri BuildSendUri(string? baseUrl, string projectId)
	{
		var normalizedBaseUrl = NormalizeOptional(baseUrl) ?? "https://fcm.googleapis.com";
		normalizedBaseUrl = normalizedBaseUrl.TrimEnd('/');
		return new Uri(
			$"{normalizedBaseUrl}/v1/projects/{Uri.EscapeDataString(projectId)}/messages:send",
			UriKind.Absolute);
	}

	private static IReadOnlyList<PushTargetModel> NormalizeTargets(IReadOnlyList<PushTargetModel> targets)
	{
		if(targets is null || targets.Count == 0)
		{
			return Array.Empty<PushTargetModel>();
		}

		var uniqueByToken = new Dictionary<string, PushTargetModel>(StringComparer.Ordinal);
		for(var i = 0; i < targets.Count; i++)
		{
			var target = targets[i];
			var normalizedToken = NormalizeOptional(target.Token);
			if(normalizedToken is null)
			{
				continue;
			}

			if(!uniqueByToken.ContainsKey(normalizedToken))
			{
				uniqueByToken[normalizedToken] = target with { Token = normalizedToken };
			}
		}

		return uniqueByToken.Values.ToList();
	}

	private static bool IsInvalidTokenResponse(HttpStatusCode statusCode, string? responseBody)
	{
		var body = responseBody ?? string.Empty;

		if(body.Contains("UNREGISTERED", StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}

		if(body.Contains("registration token is not a valid FCM registration token", StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}

		if(body.Contains("Requested entity was not found", StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}

		if(statusCode == HttpStatusCode.BadRequest
			&& body.Contains("INVALID_ARGUMENT", StringComparison.OrdinalIgnoreCase)
			&& body.Contains("token", StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}

		return false;
	}

	private static string? NormalizeOptional(string? value)
	{
		var normalized = (value ?? string.Empty).Trim();
		return normalized.Length == 0 ? null : normalized;
	}

	private static string MaskToken(string? token)
	{
		var normalized = NormalizeOptional(token);
		if(normalized is null || normalized.Length <= 8)
		{
			return "****";
		}

		return $"***{normalized[^8..]}";
	}

	private static string? TrimResponseBody(string? responseBody)
	{
		var normalized = NormalizeOptional(responseBody);
		if(normalized is null)
		{
			return null;
		}

		return normalized.Length <= 1000
			? normalized
			: normalized[..1000];
	}

	private readonly record struct SingleSendResult(bool IsSuccess, bool IsInvalidToken)
	{
		public static SingleSendResult Success() => new(true, false);
		public static SingleSendResult Fail(bool isInvalidToken) => new(false, isInvalidToken);
	}

	private sealed record FirebaseSendRequest(FirebaseMessageRequest Message);
	private sealed record FirebaseMessageRequest(
		string Token,
		FirebaseNotificationRequest Notification,
		IReadOnlyDictionary<string, string>? Data);
	private sealed record FirebaseNotificationRequest(string Title, string Body);
}