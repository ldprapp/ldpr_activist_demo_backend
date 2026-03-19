using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LdprActivistDemo.Application.Otp;

public sealed class MockOtpSender : IOtpSender
{
	private readonly ILogger<MockOtpSender> _logger;
	private readonly IHostEnvironment _environment;

	public MockOtpSender(ILogger<MockOtpSender> logger, IHostEnvironment environment)
	{
		_logger = logger;
		_environment = environment;
	}

	public Task SendAsync(string phoneNumber, string code, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		phoneNumber = (phoneNumber ?? string.Empty).Trim();

		if(_environment.IsDevelopment())
		{
			_logger.LogInformation("OTP sent to '{PhoneNumber}': {OtpCode}", phoneNumber, code);
		}
		else
		{
			_logger.LogInformation("OTP sent to '{PhoneNumber}'.", phoneNumber);
		}

		return Task.CompletedTask;
	}
}