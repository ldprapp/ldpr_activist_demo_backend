using LdprActivistDemo.Application.Diagnostics;
using LdprActivistDemo.Application.Logging;
using LdprActivistDemo.Application.Otp;
using LdprActivistDemo.Application.Users;

using Microsoft.Extensions.Logging;
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
	private readonly ILogger<PasswordResetService> _logger;

	public PasswordResetService(
		IUserService users,
		IPasswordHasher passwordHasher,
		IOtpService otp,
		IPasswordResetStore store,
		IPasswordResetRepository repo,
	   IOptions<PasswordResetOptions> options,
	   ILogger<PasswordResetService> logger)
	{
		_users = users ?? throw new ArgumentNullException(nameof(users));
		_passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
		_otp = otp ?? throw new ArgumentNullException(nameof(otp));
		_store = store ?? throw new ArgumentNullException(nameof(store));
		_repo = repo ?? throw new ArgumentNullException(nameof(repo));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	public async Task<PasswordResetIssueResult> IssueAsync(
		string phoneNumber,
		string newPassword,
		CancellationToken cancellationToken)
	{
		phoneNumber = (phoneNumber ?? string.Empty).Trim();
		newPassword = (newPassword ?? string.Empty).Trim();
		var maskedPhoneNumber = MaskPhoneNumber(phoneNumber);
		var properties = new (string Name, object? Value)[]
		{
			("PhoneNumber", maskedPhoneNumber),
		};

		using var scope = _logger.BeginExecutionScope(
			DomainLogEvents.PasswordReset.Issue,
			LogLayers.ApplicationService,
			ApplicationLogOperations.PasswordReset.Issue,
			properties);

		_logger.LogStarted(
			DomainLogEvents.PasswordReset.Issue,
			LogLayers.ApplicationService,
			ApplicationLogOperations.PasswordReset.Issue,
			"Password reset issue operation started.",
			properties);

		try
		{
			cancellationToken.ThrowIfCancellationRequested();

			var u = await _users.GetByPhoneAsync(phoneNumber, cancellationToken);
			if(u is null)
			{
				_logger.LogRejected(
					LogLevel.Warning,
					DomainLogEvents.PasswordReset.Issue,
					LogLayers.ApplicationService,
					ApplicationLogOperations.PasswordReset.Issue,
					"Password reset issue operation rejected. User not found.",
					StructuredLog.Combine(properties, ("Error", PasswordResetIssueError.UserNotFound)));
				return PasswordResetIssueResult.Fail(PasswordResetIssueError.UserNotFound);
			}

			if(!u.IsPhoneConfirmed)
			{
				_logger.LogRejected(
					LogLevel.Warning,
					DomainLogEvents.PasswordReset.Issue,
					LogLayers.ApplicationService,
					ApplicationLogOperations.PasswordReset.Issue,
					"Password reset issue operation rejected. Phone is not confirmed.",
					StructuredLog.Combine(
						properties,
						("UserId", u.Id),
						("Error", PasswordResetIssueError.PhoneNotConfirmed)));
				return PasswordResetIssueResult.Fail(PasswordResetIssueError.PhoneNotConfirmed);
			}

			var passwordHash = _passwordHasher.Hash(newPassword);
			var ttl = GetTtl();
			await _store.SetAsync(phoneNumber, new PasswordResetEntry(u.Id, passwordHash), ttl, cancellationToken);

			try
			{
				await _otp.IssueAsync(phoneNumber, cancellationToken);

				_logger.LogCompleted(
					LogLevel.Information,
					DomainLogEvents.PasswordReset.Issue,
					LogLayers.ApplicationService,
					ApplicationLogOperations.PasswordReset.Issue,
					"Password reset issue operation completed.",
					StructuredLog.Combine(
						properties,
						("UserId", u.Id),
						("TtlSeconds", (int)ttl.TotalSeconds)));

				return PasswordResetIssueResult.Success();
			}
			catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
			{
				throw;
			}
			catch(Exception ex)
			{
				await _store.RemoveAsync(phoneNumber, cancellationToken);

				_logger.LogFailed(
					LogLevel.Error,
					DomainLogEvents.PasswordReset.Issue,
					LogLayers.ApplicationService,
					ApplicationLogOperations.PasswordReset.Issue,
					"Password reset issue operation failed during OTP issuance.",
					ex,
					StructuredLog.Combine(
						properties,
						("UserId", u.Id),
						("Error", PasswordResetIssueError.OtpSendFailed)));

				return PasswordResetIssueResult.Fail(PasswordResetIssueError.OtpSendFailed);
			}
		}
		catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
		{
			_logger.LogAborted(
				LogLevel.Information,
				DomainLogEvents.PasswordReset.Issue,
				LogLayers.ApplicationService,
				ApplicationLogOperations.PasswordReset.Issue,
				"Password reset issue operation aborted.",
				properties);
			throw;
		}
		catch(Exception ex)
		{
			_logger.LogFailed(
				LogLevel.Error,
				DomainLogEvents.PasswordReset.Issue,
				LogLayers.ApplicationService,
				ApplicationLogOperations.PasswordReset.Issue,
				"Password reset issue operation failed with internal error.",
				ex,
				StructuredLog.Combine(properties, ("Error", PasswordResetIssueError.InternalError)));

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
		var maskedPhoneNumber = MaskPhoneNumber(phoneNumber);
		var properties = new (string Name, object? Value)[]
		{
			("PhoneNumber", maskedPhoneNumber),
		};

		using var scope = _logger.BeginExecutionScope(
			DomainLogEvents.PasswordReset.Confirm,
			LogLayers.ApplicationService,
			ApplicationLogOperations.PasswordReset.Confirm,
			properties);

		_logger.LogStarted(
			DomainLogEvents.PasswordReset.Confirm,
			LogLayers.ApplicationService,
			ApplicationLogOperations.PasswordReset.Confirm,
			"Password reset confirm operation started.",
			properties);

		try
		{
			cancellationToken.ThrowIfCancellationRequested();

			var otpOk = await _otp.VerifyAsync(phoneNumber, otpCode, cancellationToken);
			if(!otpOk)
			{
				_logger.LogRejected(
					LogLevel.Warning,
					DomainLogEvents.PasswordReset.Confirm,
					LogLayers.ApplicationService,
					ApplicationLogOperations.PasswordReset.Confirm,
					"Password reset confirm operation rejected. Invalid OTP.",
					StructuredLog.Combine(properties, ("Error", PasswordResetConfirmError.OtpInvalid)));
				return PasswordResetConfirmResult.Fail(PasswordResetConfirmError.OtpInvalid);
			}

			var entry = await _store.GetAsync(phoneNumber, cancellationToken);
			if(entry is null)
			{
				_logger.LogRejected(
					LogLevel.Warning,
					DomainLogEvents.PasswordReset.Confirm,
					LogLayers.ApplicationService,
					ApplicationLogOperations.PasswordReset.Confirm,
					"Password reset confirm operation rejected. Reset entry not found or expired.",
					StructuredLog.Combine(properties, ("Error", PasswordResetConfirmError.PasswordResetExpired)));
				return PasswordResetConfirmResult.Fail(PasswordResetConfirmError.PasswordResetExpired);
			}

			var u = await _users.GetByPhoneAsync(phoneNumber, cancellationToken);
			if(u is null)
			{
				await _store.RemoveAsync(phoneNumber, cancellationToken);

				_logger.LogRejected(
					LogLevel.Warning,
					DomainLogEvents.PasswordReset.Confirm,
					LogLayers.ApplicationService,
					ApplicationLogOperations.PasswordReset.Confirm,
					"Password reset confirm operation rejected. User not found.",
					StructuredLog.Combine(properties, ("Error", PasswordResetConfirmError.UserNotFound)));

				return PasswordResetConfirmResult.Fail(PasswordResetConfirmError.UserNotFound);
			}

			if(!u.IsPhoneConfirmed)
			{
				await _store.RemoveAsync(phoneNumber, cancellationToken);

				_logger.LogRejected(
					LogLevel.Warning,
					DomainLogEvents.PasswordReset.Confirm,
					LogLayers.ApplicationService,
					ApplicationLogOperations.PasswordReset.Confirm,
					"Password reset confirm operation rejected. Phone is not confirmed.",
					StructuredLog.Combine(
						properties,
						("UserId", u.Id),
						("Error", PasswordResetConfirmError.PhoneNotConfirmed)));

				return PasswordResetConfirmResult.Fail(PasswordResetConfirmError.PhoneNotConfirmed);
			}

			if(u.Id != entry.UserId)
			{
				await _store.RemoveAsync(phoneNumber, cancellationToken);

				_logger.LogRejected(
					LogLevel.Warning,
					DomainLogEvents.PasswordReset.Confirm,
					LogLayers.ApplicationService,
					ApplicationLogOperations.PasswordReset.Confirm,
					"Password reset confirm operation rejected. Reset entry mismatches current user.",
					StructuredLog.Combine(
						properties,
						("UserId", u.Id),
						("EntryUserId", entry.UserId),
						("Error", PasswordResetConfirmError.PasswordResetExpired)));

				return PasswordResetConfirmResult.Fail(PasswordResetConfirmError.PasswordResetExpired);
			}

			var updated = await _repo.SetPasswordHashAsync(u.Id, entry.PasswordHash, cancellationToken);
			await _store.RemoveAsync(phoneNumber, cancellationToken);

			if(updated)
			{
				_logger.LogCompleted(
					LogLevel.Information,
					DomainLogEvents.PasswordReset.Confirm,
					LogLayers.ApplicationService,
					ApplicationLogOperations.PasswordReset.Confirm,
					"Password reset confirm operation completed.",
					StructuredLog.Combine(properties, ("UserId", u.Id)));

				return PasswordResetConfirmResult.Success();
			}

			_logger.LogRejected(
				LogLevel.Warning,
				DomainLogEvents.PasswordReset.Confirm,
				LogLayers.ApplicationService,
				ApplicationLogOperations.PasswordReset.Confirm,
				"Password reset confirm operation rejected. User disappeared during password update.",
				StructuredLog.Combine(
					properties,
					("UserId", u.Id),
					("Error", PasswordResetConfirmError.UserNotFound)));

			return PasswordResetConfirmResult.Fail(PasswordResetConfirmError.UserNotFound);
		}
		catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
		{
			_logger.LogAborted(
				LogLevel.Information,
				DomainLogEvents.PasswordReset.Confirm,
				LogLayers.ApplicationService,
				ApplicationLogOperations.PasswordReset.Confirm,
				"Password reset confirm operation aborted.",
				properties);
			throw;
		}
		catch(Exception ex)
		{
			_logger.LogFailed(
				LogLevel.Error,
				DomainLogEvents.PasswordReset.Confirm,
				LogLayers.ApplicationService,
				ApplicationLogOperations.PasswordReset.Confirm,
				"Password reset confirm operation failed with internal error.",
				ex,
				StructuredLog.Combine(properties, ("Error", PasswordResetConfirmError.InternalError)));

			return PasswordResetConfirmResult.Fail(PasswordResetConfirmError.InternalError);
		}
	}

	private TimeSpan GetTtl()
	{
		var seconds = Math.Max(0, _options.Value.TtlSeconds);
		return seconds > 0 ? TimeSpan.FromSeconds(seconds) : DefaultTtl;
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