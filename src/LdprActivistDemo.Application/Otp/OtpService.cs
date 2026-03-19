using LdprActivistDemo.Application.Diagnostics;
using LdprActivistDemo.Application.Logging;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LdprActivistDemo.Application.Otp;

public sealed class OtpService : IOtpService
{
	private readonly IOtpStore _store;
	private readonly IOtpCodeGenerator _generator;
	private readonly IOtpSender _sender;
	private readonly IOptions<OtpOptions> _options;
	private readonly ILogger<OtpService> _logger;

	public OtpService(
		IOtpStore store,
		IOtpCodeGenerator generator,
		IOtpSender sender,
		IOptions<OtpOptions> options,
		ILogger<OtpService> logger)
	{
		_store = store;
		_generator = generator;
		_sender = sender;
		_options = options;
		_logger = logger;
	}

	public async Task IssueAsync(string phoneNumber, CancellationToken cancellationToken)
	{
		var opts = _options.Value;
		var normalizedPhoneNumber = (phoneNumber ?? string.Empty).Trim();
		var properties = new (string Name, object? Value)[]
		{
			("PhoneNumber", MaskPhoneNumber(normalizedPhoneNumber)),
			("TtlSeconds", opts.TtlSeconds),
			("Length", opts.Length),
		};

		using var scope = _logger.BeginExecutionScope(
			DomainLogEvents.Otp.Issue,
			LogLayers.ApplicationService,
			ApplicationLogOperations.Otp.Issue,
			properties);

		_logger.LogStarted(
			DomainLogEvents.Otp.Issue,
			LogLayers.ApplicationService,
			ApplicationLogOperations.Otp.Issue,
			"OTP issue operation started.",
			properties);

		try
		{
			cancellationToken.ThrowIfCancellationRequested();

			var code = _generator.GenerateDigits(opts.Length);
			await _store.SetAsync(normalizedPhoneNumber, code, TimeSpan.FromSeconds(opts.TtlSeconds), cancellationToken);
			await _sender.SendAsync(normalizedPhoneNumber, code, cancellationToken);

			_logger.LogCompleted(
				LogLevel.Information,
				DomainLogEvents.Otp.Issue,
				LogLayers.ApplicationService,
				ApplicationLogOperations.Otp.Issue,
				"OTP issue operation completed.",
				properties);
		}
		catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
		{
			_logger.LogAborted(
				LogLevel.Information,
				DomainLogEvents.Otp.Issue,
				LogLayers.ApplicationService,
				ApplicationLogOperations.Otp.Issue,
				"OTP issue operation aborted.",
				properties);
			throw;
		}
		catch(Exception ex)
		{
			_logger.LogFailed(
				LogLevel.Error,
				DomainLogEvents.Otp.Issue,
				LogLayers.ApplicationService,
				ApplicationLogOperations.Otp.Issue,
				"OTP issue operation failed.",
				ex,
				properties);
			throw;
		}
	}

	public async Task<bool> VerifyAsync(string phoneNumber, string code, CancellationToken cancellationToken)
	{
		var normalizedPhoneNumber = (phoneNumber ?? string.Empty).Trim();
		var normalizedCode = (code ?? string.Empty).Trim();
		var properties = new (string Name, object? Value)[]
		{
			("PhoneNumber", MaskPhoneNumber(normalizedPhoneNumber)),
		};

		using var scope = _logger.BeginExecutionScope(
			DomainLogEvents.Otp.Verify,
			LogLayers.ApplicationService,
			ApplicationLogOperations.Otp.Verify,
			properties);

		_logger.LogStarted(
			DomainLogEvents.Otp.Verify,
			LogLayers.ApplicationService,
			ApplicationLogOperations.Otp.Verify,
			"OTP verification operation started.",
			properties);

		try
		{
			cancellationToken.ThrowIfCancellationRequested();

			var existing = await _store.GetAsync(normalizedPhoneNumber, cancellationToken);
			if(existing is null)
			{
				_logger.LogRejected(
					LogLevel.Warning,
					DomainLogEvents.Otp.Verify,
					LogLayers.ApplicationService,
					ApplicationLogOperations.Otp.Verify,
					"OTP verification operation rejected. OTP not found or expired.",
					properties);
				return false;
			}

			if(!StringComparer.Ordinal.Equals(existing, normalizedCode))
			{
				_logger.LogRejected(
					LogLevel.Warning,
					DomainLogEvents.Otp.Verify,
					LogLayers.ApplicationService,
					ApplicationLogOperations.Otp.Verify,
					"OTP verification operation rejected. Invalid code.",
					properties);
				return false;
			}

			await _store.RemoveAsync(normalizedPhoneNumber, cancellationToken);

			_logger.LogCompleted(
				LogLevel.Information,
				DomainLogEvents.Otp.Verify,
				LogLayers.ApplicationService,
				ApplicationLogOperations.Otp.Verify,
				"OTP verification operation completed.",
				properties);
			return true;
		}
		catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
		{
			_logger.LogAborted(
				LogLevel.Information,
				DomainLogEvents.Otp.Verify,
				LogLayers.ApplicationService,
				ApplicationLogOperations.Otp.Verify,
				"OTP verification operation aborted.",
				properties);
			throw;
		}
		catch(Exception ex)
		{
			_logger.LogFailed(
				LogLevel.Error,
				DomainLogEvents.Otp.Verify,
				LogLayers.ApplicationService,
				ApplicationLogOperations.Otp.Verify,
				"OTP verification operation failed.",
				ex,
				properties);
			throw;
		}

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
}