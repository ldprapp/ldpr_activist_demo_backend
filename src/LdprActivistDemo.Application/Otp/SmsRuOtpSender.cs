using System.Text.Json;
using System.Text.Json.Serialization;

using LdprActivistDemo.Application.Diagnostics;
using LdprActivistDemo.Application.Logging;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LdprActivistDemo.Application.Otp;

public sealed class SmsRuOtpSender : IOtpSender
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	private readonly HttpClient _httpClient;
	private readonly IOptions<SmsRuOptions> _options;
	private readonly ILogger<SmsRuOtpSender> _logger;

	public SmsRuOtpSender(
		HttpClient httpClient,
		IOptions<SmsRuOptions> options,
		ILogger<SmsRuOtpSender> logger)
	{
		_httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	public async Task SendAsync(string phoneNumber, string code, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var options = _options.Value;
		var normalizedPhoneNumber = NormalizePhoneNumberForSmsRu(phoneNumber);
		var normalizedCode = (code ?? string.Empty).Trim();

		var properties = new (string Name, object? Value)[]
		{
			("PhoneNumber", MaskPhoneNumber(normalizedPhoneNumber)),
			("IsTestMode", options.IsTestMode),
		};

		using var scope = _logger.BeginExecutionScope(
			DomainLogEvents.Otp.Send,
			LogLayers.ApplicationService,
			ApplicationLogOperations.Otp.SendSmsRu,
			properties);

		_logger.LogStarted(
			DomainLogEvents.Otp.Send,
			LogLayers.ApplicationService,
			ApplicationLogOperations.Otp.SendSmsRu,
			"SMS.ru OTP sender operation started.",
			properties);

		try
		{
			if(string.IsNullOrWhiteSpace(options.ApiId))
			{
				throw new InvalidOperationException("SMS.ru api_id is not configured.");
			}

			if(string.IsNullOrWhiteSpace(normalizedPhoneNumber))
			{
				throw new InvalidOperationException("Phone number is empty after normalization for SMS.ru.");
			}

			if(string.IsNullOrWhiteSpace(normalizedCode))
			{
				throw new InvalidOperationException("OTP code is empty.");
			}

			using var content = new FormUrlEncodedContent(
				new Dictionary<string, string>
				{
					["api_id"] = options.ApiId.Trim(),
					["to"] = normalizedPhoneNumber,
					["msg"] = normalizedCode,
					["json"] = "1",
					["test"] = options.IsTestMode ? "1" : "0",
				});

			using var response = await _httpClient.PostAsync("/sms/send", content, cancellationToken);
			var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

			if(!response.IsSuccessStatusCode)
			{
				throw new InvalidOperationException(
					$"SMS.ru returned HTTP {(int)response.StatusCode}: {responseBody}");
			}

			var payload = JsonSerializer.Deserialize<SmsRuSendResponse>(responseBody, JsonOptions);
			if(payload is null)
			{
				throw new InvalidOperationException("SMS.ru returned empty or invalid JSON response.");
			}

			if(!string.Equals(payload.Status, "OK", StringComparison.OrdinalIgnoreCase))
			{
				throw new InvalidOperationException(
					$"SMS.ru request failed. status_code={payload.StatusCode}, status_text={payload.StatusText}");
			}

			var smsItem = payload.Sms?.Values.FirstOrDefault();
			if(smsItem is null)
			{
				throw new InvalidOperationException("SMS.ru response does not contain per-phone delivery result.");
			}

			if(!string.Equals(smsItem.Status, "OK", StringComparison.OrdinalIgnoreCase))
			{
				throw new InvalidOperationException(
					$"SMS.ru SMS send failed. status_code={smsItem.StatusCode}, status_text={smsItem.StatusText}");
			}

			_logger.LogCompleted(
				LogLevel.Information,
				DomainLogEvents.Otp.Send,
				LogLayers.ApplicationService,
				ApplicationLogOperations.Otp.SendSmsRu,
				"SMS.ru OTP sender operation completed.",
				StructuredLog.Combine(
					properties,
					("ProviderStatusCode", smsItem.StatusCode),
					("ProviderSmsId", smsItem.SmsId),
					("ProviderBalance", payload.Balance)));
		}
		catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
		{
			_logger.LogAborted(
				LogLevel.Information,
				DomainLogEvents.Otp.Send,
				LogLayers.ApplicationService,
				ApplicationLogOperations.Otp.SendSmsRu,
				"SMS.ru OTP sender operation aborted.",
				properties);
			throw;
		}
		catch(Exception ex)
		{
			_logger.LogFailed(
				LogLevel.Error,
				DomainLogEvents.Otp.Send,
				LogLayers.ApplicationService,
				ApplicationLogOperations.Otp.SendSmsRu,
				"SMS.ru OTP sender operation failed.",
				ex,
				properties);
			throw;
		}
	}

	private static string NormalizePhoneNumberForSmsRu(string? value)
	{
		if(string.IsNullOrWhiteSpace(value))
		{
			return string.Empty;
		}

		var buffer = new char[value.Length];
		var count = 0;

		for(var i = 0; i < value.Length; i++)
		{
			var ch = value[i];
			if(ch >= '0' && ch <= '9')
			{
				buffer[count++] = ch;
			}
		}

		return count == 0
			? string.Empty
			: new string(buffer, 0, count);
	}

	private static string MaskPhoneNumber(string? value)
	{
		var normalized = (value ?? string.Empty).Trim();
		if(normalized.Length <= 4)
		{
			return "****";
		}

		return $"***{normalized[^4..]}";
	}

	private sealed class SmsRuSendResponse
	{
		[JsonPropertyName("status")]
		public string? Status { get; set; }

		[JsonPropertyName("status_code")]
		public int? StatusCode { get; set; }

		[JsonPropertyName("status_text")]
		public string? StatusText { get; set; }

		[JsonPropertyName("balance")]
		public decimal? Balance { get; set; }

		[JsonPropertyName("sms")]
		public Dictionary<string, SmsRuSendItem>? Sms { get; set; }
	}

	private sealed class SmsRuSendItem
	{
		[JsonPropertyName("status")]
		public string? Status { get; set; }

		[JsonPropertyName("status_code")]
		public int? StatusCode { get; set; }

		[JsonPropertyName("status_text")]
		public string? StatusText { get; set; }

		[JsonPropertyName("sms_id")]
		public string? SmsId { get; set; }
	}
}