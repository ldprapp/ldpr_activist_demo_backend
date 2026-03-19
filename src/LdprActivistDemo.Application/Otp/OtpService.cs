using Microsoft.Extensions.Options;

namespace LdprActivistDemo.Application.Otp;

public sealed class OtpService : IOtpService
{
	private readonly IOtpStore _store;
	private readonly IOtpCodeGenerator _generator;
	private readonly IOtpSender _sender;
	private readonly IOptions<OtpOptions> _options;

	public OtpService(
		IOtpStore store,
		IOtpCodeGenerator generator,
		IOtpSender sender,
		IOptions<OtpOptions> options)
	{
		_store = store;
		_generator = generator;
		_sender = sender;
		_options = options;
	}

	public async Task IssueAsync(string phoneNumber, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var opts = _options.Value;

		phoneNumber = (phoneNumber ?? string.Empty).Trim();
		cancellationToken.ThrowIfCancellationRequested();

		var code = _generator.GenerateDigits(opts.Length);

		await _store.SetAsync(phoneNumber, code, TimeSpan.FromSeconds(opts.TtlSeconds), cancellationToken);

		await _sender.SendAsync(phoneNumber, code, cancellationToken);
	}

	public async Task<bool> VerifyAsync(string phoneNumber, string code, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

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

		cancellationToken.ThrowIfCancellationRequested();
		await _store.RemoveAsync(phoneNumber, cancellationToken);
		return true;
	}
}