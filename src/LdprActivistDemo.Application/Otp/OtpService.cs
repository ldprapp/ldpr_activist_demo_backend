using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LdprActivistDemo.Application.Otp;

public sealed class OtpService : IOtpService
{
	private readonly IOtpStore _store;
	private readonly IOtpCodeGenerator _generator;
	private readonly IOptions<OtpOptions> _options;
	private readonly ILogger<OtpService> _logger;
	private readonly IHostEnvironment _environment;

	public OtpService(
		IOtpStore store,
		IOtpCodeGenerator generator,
		IOptions<OtpOptions> options,
		ILogger<OtpService> logger,
		IHostEnvironment environment)
	{
		_store = store;
		_generator = generator;
		_options = options;
		_logger = logger;
		_environment = environment;
	}

	public async Task<string> IssueAsync(string phoneNumber, CancellationToken cancellationToken)
	{
		var opts = _options.Value;

		phoneNumber = (phoneNumber ?? string.Empty).Trim();
		var code = _generator.GenerateDigits(opts.Length);

		await _store.SetAsync(phoneNumber, code, TimeSpan.FromSeconds(opts.TtlSeconds), cancellationToken);

		if(_environment.IsDevelopment())
		{
			_logger.LogInformation("OTP issued for '{PhoneNumber}': {OtpCode}", phoneNumber, code);
		}
		else
		{
			_logger.LogInformation("OTP issued for '{PhoneNumber}'.", phoneNumber);
		}

		return code;
	}

	public async Task<bool> VerifyAsync(string phoneNumber, string code, CancellationToken cancellationToken)
	{
		phoneNumber = (phoneNumber ?? string.Empty).Trim();
		code = (code ?? string.Empty).Trim();

		var existing = await _store.GetAsync(phoneNumber, cancellationToken);
		if(existing is null)
		{
			return false;
		}

		if(!StringComparer.Ordinal.Equals(existing, code))
		{
			return false;
		}

		await _store.RemoveAsync(phoneNumber, cancellationToken);
		return true;
	}
}