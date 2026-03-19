using LdprActivistDemo.Application.Diagnostics;
using LdprActivistDemo.Application.Logging;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LdprActivistDemo.Application.Otp;

public sealed class MockOtpSender : IOtpSender
{
	private readonly ILogger<MockOtpSender> _logger;
	private readonly IHostEnvironment _environment;

	public MockOtpSender(ILogger<MockOtpSender> logger, IHostEnvironment environment)
	{
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_environment = environment ?? throw new ArgumentNullException(nameof(environment));
	}

	public Task SendAsync(string phoneNumber, string code, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var normalizedPhoneNumber = (phoneNumber ?? string.Empty).Trim();
		var properties = new (string Name, object? Value)[]
		{
			("PhoneNumber", MaskPhoneNumber(normalizedPhoneNumber)),
			("IsDevelopment", _environment.IsDevelopment()),
		};

		using var scope = _logger.BeginExecutionScope(
			DomainLogEvents.Otp.Send,
			LogLayers.ApplicationService,
			ApplicationLogOperations.Otp.SendMock,
			properties);

		_logger.LogStarted(
			DomainLogEvents.Otp.Send,
			LogLayers.ApplicationService,
			ApplicationLogOperations.Otp.SendMock,
			"OTP sender operation started.",
			properties);

		if(_environment.IsDevelopment())
		{
			_logger.LogCompleted(
				LogLevel.Information,
				DomainLogEvents.Otp.Send,
				LogLayers.ApplicationService,
				ApplicationLogOperations.Otp.SendMock,
				"OTP sender operation completed in development mode.",
				StructuredLog.Combine(
					properties,
					("OtpCode", code)));
		}
		else
		{
			_logger.LogCompleted(
				LogLevel.Information,
				DomainLogEvents.Otp.Send,
				LogLayers.ApplicationService,
				ApplicationLogOperations.Otp.SendMock,
				"OTP sender operation completed.",
				properties);
		}

		return Task.CompletedTask;
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