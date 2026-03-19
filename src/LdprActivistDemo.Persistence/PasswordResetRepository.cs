using LdprActivistDemo.Application.Diagnostics;
using LdprActivistDemo.Application.Logging;
using LdprActivistDemo.Application.PasswordReset;
using LdprActivistDemo.Persistence.Logging;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LdprActivistDemo.Persistence;

public sealed class PasswordResetRepository : IPasswordResetRepository
{
	private readonly AppDbContext _db;
	private readonly ILogger<PasswordResetRepository> _logger;

	public PasswordResetRepository(AppDbContext db, ILogger<PasswordResetRepository> logger)
	{
		_db = db ?? throw new ArgumentNullException(nameof(db));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	public async Task<bool> SetPasswordHashAsync(Guid userId, string passwordHash, CancellationToken cancellationToken)
	{
		var properties = new (string Name, object? Value)[]
		{
			("UserId", userId),
		};

		using var scope = _logger.BeginExecutionScope(
			DomainLogEvents.PasswordReset.Repository.UpdatePasswordHash,
			LogLayers.PersistenceRepository,
			PersistenceLogOperations.PasswordReset.SetPasswordHash,
			properties);

		_logger.LogStarted(
			DomainLogEvents.PasswordReset.Repository.UpdatePasswordHash,
			LogLayers.PersistenceRepository,
			PersistenceLogOperations.PasswordReset.SetPasswordHash,
			"Password hash update started.",
			properties);

		try
		{
			if(userId == Guid.Empty || string.IsNullOrWhiteSpace(passwordHash))
			{
				_logger.LogRejected(
					LogLevel.Warning,
					DomainLogEvents.PasswordReset.Repository.UpdatePasswordHash,
					LogLayers.PersistenceRepository,
					PersistenceLogOperations.PasswordReset.SetPasswordHash,
					"Password hash update rejected by validation.",
					StructuredLog.Combine(properties, ("RejectedReason", "ValidationFailed")));

				return false;
			}

			var affected = await _db.Users
				.Where(x => x.Id == userId && x.IsPhoneConfirmed)
				.ExecuteUpdateAsync(
					setters => setters.SetProperty(x => x.PasswordHash, passwordHash),
					cancellationToken);

			if(affected <= 0)
			{
				_logger.LogRejected(
					LogLevel.Warning,
					DomainLogEvents.PasswordReset.Repository.UpdatePasswordHash,
					LogLayers.PersistenceRepository,
					PersistenceLogOperations.PasswordReset.SetPasswordHash,
					"Password hash update rejected. Target user not found or not confirmed.",
					StructuredLog.Combine(properties, ("Updated", false)));

				return false;
			}

			_logger.LogCompleted(
				LogLevel.Information,
				DomainLogEvents.PasswordReset.Repository.UpdatePasswordHash,
				LogLayers.PersistenceRepository,
				PersistenceLogOperations.PasswordReset.SetPasswordHash,
				"Password hash update completed.",
				StructuredLog.Combine(properties, ("Updated", true)));

			return true;
		}
		catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
		{
			_logger.LogAborted(
				LogLevel.Information,
				DomainLogEvents.PasswordReset.Repository.UpdatePasswordHash,
				LogLayers.PersistenceRepository,
				PersistenceLogOperations.PasswordReset.SetPasswordHash,
				"Password hash update aborted.",
				properties);
			throw;
		}
		catch(Exception ex)
		{
			_logger.LogFailed(
				LogLevel.Error,
				DomainLogEvents.PasswordReset.Repository.UpdatePasswordHash,
				LogLayers.PersistenceRepository,
				PersistenceLogOperations.PasswordReset.SetPasswordHash,
				"Password hash update failed.",
				ex,
				properties);
			throw;
		}
	}
}