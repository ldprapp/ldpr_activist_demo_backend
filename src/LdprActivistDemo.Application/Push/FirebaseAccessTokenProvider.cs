using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using LdprActivistDemo.Application.Diagnostics;
using LdprActivistDemo.Application.Logging;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LdprActivistDemo.Application.Push;

public interface IFirebaseAccessTokenProvider
{
	Task<string> GetAccessTokenAsync(CancellationToken cancellationToken);
}

internal sealed class FirebaseAccessTokenProvider : IFirebaseAccessTokenProvider
{
	private const string EventName = "push.sender.access_token";
	private const string OperationName = "push.sender.firebase.access_token.acquire";
	private const string FirebaseMessagingScope = "https://www.googleapis.com/auth/firebase.messaging";
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
	private static readonly TimeSpan RefreshSkew = TimeSpan.FromMinutes(1);

	private readonly System.Net.Http.HttpClient _httpClient;
	private readonly IConfiguration _configuration;
	private readonly ILogger<FirebaseAccessTokenProvider> _logger;
	private readonly SemaphoreSlim _gate = new(1, 1);

	private string? _cachedAccessToken;
	private DateTimeOffset _cachedAccessTokenExpiresAtUtc;

	public FirebaseAccessTokenProvider(
		System.Net.Http.HttpClient httpClient,
		IConfiguration configuration,
		ILogger<FirebaseAccessTokenProvider> logger)
	{
		_httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
		_configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
	{
		var settings = FirebasePushSettings.Load(_configuration);
		var properties = new (string Name, object? Value)[]
		{
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
			"Firebase access token acquisition started.",
			properties);

		try
		{
			if(!settings.Enabled)
			{
				_logger.LogRejected(
					LogLevel.Warning,
					EventName,
					LogLayers.ApplicationService,
					OperationName,
					"Firebase access token acquisition rejected because push sending is disabled in configuration.",
					properties);

				throw new InvalidOperationException("Firebase push sending is disabled.");
			}

			if(HasFreshCachedToken())
			{
				_logger.LogCompleted(
					LogLevel.Debug,
					EventName,
					LogLayers.ApplicationService,
					OperationName,
					"Firebase access token returned from cache.",
					StructuredLog.Combine(
						properties,
						("ExpiresAtUtc", _cachedAccessTokenExpiresAtUtc)));

				return _cachedAccessToken!;
			}

			await _gate.WaitAsync(cancellationToken);
			try
			{
				if(HasFreshCachedToken())
				{
					_logger.LogCompleted(
						LogLevel.Debug,
						EventName,
						LogLayers.ApplicationService,
						OperationName,
						"Firebase access token returned from cache after lock acquisition.",
						StructuredLog.Combine(
							properties,
							("ExpiresAtUtc", _cachedAccessTokenExpiresAtUtc)));

					return _cachedAccessToken!;
				}

				var credentials = await LoadCredentialsAsync(settings, cancellationToken);
				var accessTokenResponse = await RequestAccessTokenAsync(credentials, cancellationToken);

				_cachedAccessToken = accessTokenResponse.AccessToken;
				_cachedAccessTokenExpiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(
					Math.Max(60, accessTokenResponse.ExpiresIn - 60));

				_logger.LogCompleted(
					LogLevel.Debug,
					EventName,
					LogLayers.ApplicationService,
					OperationName,
					"Firebase access token acquired successfully.",
					StructuredLog.Combine(
						properties,
						("ExpiresAtUtc", _cachedAccessTokenExpiresAtUtc)));

				return _cachedAccessToken!;
			}
			finally
			{
				_gate.Release();
			}
		}
		catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
		{
			_logger.LogAborted(
				LogLevel.Information,
				EventName,
				LogLayers.ApplicationService,
				OperationName,
				"Firebase access token acquisition aborted.",
				properties);
			throw;
		}
		catch(Exception ex)
		{
			_logger.LogFailed(
				LogLevel.Error,
				EventName,
				LogLayers.ApplicationService,
				OperationName,
				"Firebase access token acquisition failed.",
				ex,
				properties);
			throw;
		}
	}

	private bool HasFreshCachedToken()
	{
		return !string.IsNullOrWhiteSpace(_cachedAccessToken)
			&& _cachedAccessTokenExpiresAtUtc > DateTimeOffset.UtcNow.Add(RefreshSkew);
	}

	private async Task<FirebaseServiceAccountCredentials> LoadCredentialsAsync(
		FirebasePushSettings settings,
		CancellationToken cancellationToken)
	{
		var rawJson = settings.ServiceAccountJson;
		if(string.IsNullOrWhiteSpace(rawJson))
		{
			if(string.IsNullOrWhiteSpace(settings.ServiceAccountJsonPath))
			{
				throw new InvalidOperationException(
					"FirebasePush configuration must contain ServiceAccountJson or ServiceAccountJsonPath.");
			}

			var path = Path.IsPathRooted(settings.ServiceAccountJsonPath)
				? settings.ServiceAccountJsonPath
				: Path.GetFullPath(settings.ServiceAccountJsonPath, AppContext.BaseDirectory);

			if(!File.Exists(path))
			{
				throw new FileNotFoundException(
					"Firebase service account json file was not found.",
					path);
			}

			rawJson = await File.ReadAllTextAsync(path, cancellationToken);
		}

		var document = JsonSerializer.Deserialize<FirebaseServiceAccountDocument>(rawJson, JsonOptions)
			?? throw new InvalidOperationException("Firebase service account json is invalid.");

		var clientEmail = NormalizeOptional(document.ClientEmail)
			?? throw new InvalidOperationException("Firebase service account json does not contain client_email.");
		var privateKey = NormalizeOptional(document.PrivateKey)
			?? throw new InvalidOperationException("Firebase service account json does not contain private_key.");
		var tokenUri = NormalizeOptional(document.TokenUri)
			?? "https://oauth2.googleapis.com/token";

		return new FirebaseServiceAccountCredentials(clientEmail, privateKey, tokenUri);
	}

	private async Task<GoogleOAuthAccessTokenResponse> RequestAccessTokenAsync(
		FirebaseServiceAccountCredentials credentials,
		CancellationToken cancellationToken)
	{
		var issuedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		var expiresAt = issuedAt + 3600;
		var assertion = BuildJwtAssertion(credentials, issuedAt, expiresAt);

		using var request = new HttpRequestMessage(HttpMethod.Post, credentials.TokenUri)
		{
			Content = new FormUrlEncodedContent(new Dictionary<string, string>(StringComparer.Ordinal)
			{
				["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
				["assertion"] = assertion,
			}),
		};

		using var response = await _httpClient.SendAsync(request, cancellationToken);
		var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

		if(!response.IsSuccessStatusCode)
		{
			throw new InvalidOperationException(
				$"Firebase OAuth token endpoint returned {(int)response.StatusCode}: {responseBody}");
		}

		var tokenResponse = JsonSerializer.Deserialize<GoogleOAuthAccessTokenResponse>(responseBody, JsonOptions)
			?? throw new InvalidOperationException("Firebase OAuth token response is invalid.");

		if(string.IsNullOrWhiteSpace(tokenResponse.AccessToken) || tokenResponse.ExpiresIn <= 0)
		{
			throw new InvalidOperationException("Firebase OAuth token response does not contain a valid access token.");
		}

		return tokenResponse;
	}

	private static string BuildJwtAssertion(
		FirebaseServiceAccountCredentials credentials,
		long issuedAt,
		long expiresAt)
	{
		var headerJson = JsonSerializer.Serialize(
			new Dictionary<string, object?>(StringComparer.Ordinal)
			{
				["alg"] = "RS256",
				["typ"] = "JWT",
			},
			JsonOptions);

		var payloadJson = JsonSerializer.Serialize(
			new Dictionary<string, object?>(StringComparer.Ordinal)
			{
				["iss"] = credentials.ClientEmail,
				["scope"] = FirebaseMessagingScope,
				["aud"] = credentials.TokenUri,
				["iat"] = issuedAt,
				["exp"] = expiresAt,
			},
			JsonOptions);

		var unsignedToken =
			$"{Base64UrlEncode(Encoding.UTF8.GetBytes(headerJson))}.{Base64UrlEncode(Encoding.UTF8.GetBytes(payloadJson))}";

		using var rsa = RSA.Create();
		rsa.ImportFromPem(credentials.PrivateKey);

		var signature = rsa.SignData(
			Encoding.UTF8.GetBytes(unsignedToken),
			HashAlgorithmName.SHA256,
			RSASignaturePadding.Pkcs1);

		return $"{unsignedToken}.{Base64UrlEncode(signature)}";
	}

	private static string Base64UrlEncode(byte[] bytes)
	{
		return Convert.ToBase64String(bytes)
			.TrimEnd('=')
			.Replace('+', '-')
			.Replace('/', '_');
	}

	private static string? NormalizeOptional(string? value)
	{
		var normalized = (value ?? string.Empty).Trim();
		return normalized.Length == 0 ? null : normalized;
	}

	private sealed record FirebaseServiceAccountDocument(
		[property: JsonPropertyName("project_id")]
		string? ProjectId,
		[property: JsonPropertyName("client_email")]
		string? ClientEmail,
		[property: JsonPropertyName("private_key")]
		string? PrivateKey,
		[property: JsonPropertyName("token_uri")]
		string? TokenUri);

	private sealed record FirebaseServiceAccountCredentials(
		string ClientEmail,
		string PrivateKey,
		string TokenUri);

	private sealed record GoogleOAuthAccessTokenResponse(
		[property: JsonPropertyName("access_token")]
		string AccessToken,
		[property: JsonPropertyName("expires_in")]
		int ExpiresIn,
		[property: JsonPropertyName("token_type")]
		string TokenType);
}