using LdprActivistDemo.Application.Otp;
using LdprActivistDemo.Application.Users;

using Microsoft.Extensions.Options;

namespace LdprActivistDemo.Application.PasswordReset;

public sealed class PasswordResetService : IPasswordResetService
{
	private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(10);

	private readonly IUserService _users;
	private readonly IPasswordHasher _passwordHasher;
	private readonly IOtpService _otp;
	private readonly IPasswordResetStore _store;
	private readonly IPasswordResetRepository _repo;
	private readonly IOptions<PasswordResetOptions> _options;

	public PasswordResetService(
		IUserService users,
		IPasswordHasher passwordHasher,
		IOtpService otp,
		IPasswordResetStore store,
		IPasswordResetRepository repo,
		IOptions<PasswordResetOptions> options)
	{
		_users = users ?? throw new ArgumentNullException(nameof(users));
		_passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
		_otp = otp ?? throw new ArgumentNullException(nameof(otp));
		_store = store ?? throw new ArgumentNullException(nameof(store));
		_repo = repo ?? throw new ArgumentNullException(nameof(repo));
		_options = options ?? throw new ArgumentNullException(nameof(options));
	}

	public async Task<PasswordResetIssueResult> IssueAsync(
		string phoneNumber,
		string newPassword,
		CancellationToken cancellationToken)
	{
		phoneNumber = (phoneNumber ?? string.Empty).Trim();
		newPassword = (newPassword ?? string.Empty).Trim();

		try
		{
			var u = await _users.GetByPhoneAsync(phoneNumber, cancellationToken);
			if(u is null)
			{
				return PasswordResetIssueResult.Fail(PasswordResetIssueError.UserNotFound);
			}

			if(!u.IsPhoneConfirmed)
			{
				return PasswordResetIssueResult.Fail(PasswordResetIssueError.PhoneNotConfirmed);
			}

			var passwordHash = _passwordHasher.Hash(newPassword);
			var ttl = GetTtl();

			await _store.SetAsync(phoneNumber, new PasswordResetEntry(u.Id, passwordHash), ttl, cancellationToken);

			try
			{
				await _otp.IssueAsync(phoneNumber, cancellationToken);
				return PasswordResetIssueResult.Success();
			}
			catch
			{
				await _store.RemoveAsync(phoneNumber, cancellationToken);
				return PasswordResetIssueResult.Fail(PasswordResetIssueError.OtpSendFailed);
			}
		}
		catch
		{
			return PasswordResetIssueResult.Fail(PasswordResetIssueError.InternalError);
		}
	}

	public async Task<PasswordResetConfirmResult> ConfirmAsync(
		string phoneNumber,
		string otpCode,
		CancellationToken cancellationToken)
	{
		phoneNumber = (phoneNumber ?? string.Empty).Trim();
		otpCode = (otpCode ?? string.Empty).Trim();

		try
		{
			var otpOk = await _otp.VerifyAsync(phoneNumber, otpCode, cancellationToken);
			if(!otpOk)
			{
				return PasswordResetConfirmResult.Fail(PasswordResetConfirmError.OtpInvalid);
			}

			var entry = await _store.GetAsync(phoneNumber, cancellationToken);
			if(entry is null)
			{
				return PasswordResetConfirmResult.Fail(PasswordResetConfirmError.PasswordResetExpired);
			}

			var u = await _users.GetByPhoneAsync(phoneNumber, cancellationToken);
			if(u is null)
			{
				await _store.RemoveAsync(phoneNumber, cancellationToken);
				return PasswordResetConfirmResult.Fail(PasswordResetConfirmError.UserNotFound);
			}

			if(!u.IsPhoneConfirmed)
			{
				await _store.RemoveAsync(phoneNumber, cancellationToken);
				return PasswordResetConfirmResult.Fail(PasswordResetConfirmError.PhoneNotConfirmed);
			}

			if(u.Id != entry.UserId)
			{
				await _store.RemoveAsync(phoneNumber, cancellationToken);
				return PasswordResetConfirmResult.Fail(PasswordResetConfirmError.PasswordResetExpired);
			}

			var updated = await _repo.SetPasswordHashAsync(u.Id, entry.PasswordHash, cancellationToken);
			await _store.RemoveAsync(phoneNumber, cancellationToken);

			return updated
				? PasswordResetConfirmResult.Success()
				: PasswordResetConfirmResult.Fail(PasswordResetConfirmError.UserNotFound);
		}
		catch
		{
			return PasswordResetConfirmResult.Fail(PasswordResetConfirmError.InternalError);
		}
	}

	private TimeSpan GetTtl()
	{
		var seconds = Math.Max(0, _options.Value.TtlSeconds);
		return seconds > 0 ? TimeSpan.FromSeconds(seconds) : DefaultTtl;
	}
}