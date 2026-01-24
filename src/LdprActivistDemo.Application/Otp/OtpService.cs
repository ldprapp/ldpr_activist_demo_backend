using Microsoft.Extensions.Options;

namespace LdprActivistDemo.Application.Otp;

public sealed class OtpService : IOtpService
{
	private readonly IOtpStore _store;
	private readonly IOtpCodeGenerator _generator;
	private readonly IOptions<OtpOptions> _options;

	public OtpService(
		IOtpStore store,
		IOtpCodeGenerator generator,
		IOptions<OtpOptions> options)
	{
		_store = store;
		_generator = generator;
		_options = options;
	}

	public async Task<string> IssueAsync(string phoneNumber, CancellationToken cancellationToken)
	{
		var opts = _options.Value;
		var code = _generator.GenerateDigits(opts.Length);
		await _store.SetAsync(phoneNumber, code, TimeSpan.FromSeconds(opts.TtlSeconds), cancellationToken);
		return code;
	}

	public async Task<bool> VerifyAsync(string phoneNumber, string code, CancellationToken cancellationToken)
	{
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