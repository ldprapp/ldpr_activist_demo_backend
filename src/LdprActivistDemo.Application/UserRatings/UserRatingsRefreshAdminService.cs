using LdprActivistDemo.Application.Diagnostics;
using LdprActivistDemo.Application.Logging;
using LdprActivistDemo.Application.UserRatings.Models;
using LdprActivistDemo.Application.Users;
using LdprActivistDemo.Application.Users.Models;

using Microsoft.Extensions.Logging;

namespace LdprActivistDemo.Application.UserRatings;

/// <summary>
/// Административный сервис управления расписанием и ручным запуском пересчёта рейтингов.
/// </summary>
public sealed class UserRatingsRefreshAdminService : IUserRatingsRefreshAdminService
{
	private readonly IActorAccessService _actorAccess;
	private readonly IUserRatingsRefreshRuntime _runtime;
	private readonly ILogger<UserRatingsRefreshAdminService> _logger;

	public UserRatingsRefreshAdminService(
		IActorAccessService actorAccess,
		IUserRatingsRefreshRuntime runtime,
		ILogger<UserRatingsRefreshAdminService> logger)
	{
		_actorAccess = actorAccess ?? throw new ArgumentNullException(nameof(actorAccess));
		_runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	public async Task<UserRatingsAdminResult<UserRatingsRefreshScheduleModel>> GetRefreshScheduleAsync(
		Guid actorUserId,
		string actorUserPassword,
		CancellationToken cancellationToken)
	{
		var properties = new (string Name, object? Value)[]
		{
			("ActorUserId", actorUserId),
		};

		using var scope = _logger.BeginExecutionScope(
			DomainLogEvents.UserRatings.GetRefreshSchedule,
			LogLayers.ApplicationService,
			ApplicationLogOperations.UserRatings.GetRefreshSchedule,
			properties);

		_logger.LogStarted(
			DomainLogEvents.UserRatings.GetRefreshSchedule,
			LogLayers.ApplicationService,
			ApplicationLogOperations.UserRatings.GetRefreshSchedule,
			"User ratings refresh schedule read started.",
			properties);

		try
		{
			var authError = await AuthorizeAdminAsync(actorUserId, actorUserPassword, cancellationToken);
			if(authError is not null)
			{
				_logger.LogRejected(
					LogLevel.Warning,
					DomainLogEvents.UserRatings.GetRefreshSchedule,
					LogLayers.ApplicationService,
					ApplicationLogOperations.UserRatings.GetRefreshSchedule,
					"User ratings refresh schedule read rejected.",
					StructuredLog.Combine(properties, ("Error", authError.Value)));

				return UserRatingsAdminResult<UserRatingsRefreshScheduleModel>.Fail(authError.Value);
			}

			var schedule = await _runtime.GetScheduleAsync(cancellationToken);

			_logger.LogCompleted(
				LogLevel.Information,
				DomainLogEvents.UserRatings.GetRefreshSchedule,
				LogLayers.ApplicationService,
				ApplicationLogOperations.UserRatings.GetRefreshSchedule,
				"User ratings refresh schedule read completed.",
				StructuredLog.Combine(properties, ("Hour", schedule.Hour), ("Minute", schedule.Minute)));

			return UserRatingsAdminResult<UserRatingsRefreshScheduleModel>.Ok(schedule);
		}
		catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
		{
			_logger.LogAborted(
				LogLevel.Information,
				DomainLogEvents.UserRatings.GetRefreshSchedule,
				LogLayers.ApplicationService,
				ApplicationLogOperations.UserRatings.GetRefreshSchedule,
				"User ratings refresh schedule read aborted.",
				properties);
			throw;
		}
		catch(Exception ex)
		{
			_logger.LogFailed(
				LogLevel.Error,
				DomainLogEvents.UserRatings.GetRefreshSchedule,
				LogLayers.ApplicationService,
				ApplicationLogOperations.UserRatings.GetRefreshSchedule,
				"User ratings refresh schedule read failed.",
				ex,
				properties);
			throw;
		}
	}

	public async Task<UserRatingsAdminResult<UserRatingsRefreshScheduleModel>> UpdateRefreshScheduleAsync(
		Guid actorUserId,
		string actorUserPassword,
		int hour,
		int minute,
		CancellationToken cancellationToken)
	{
		var properties = new (string Name, object? Value)[]
		{
			("ActorUserId", actorUserId),
			("Hour", hour),
			("Minute", minute),
		};

		using var scope = _logger.BeginExecutionScope(
			DomainLogEvents.UserRatings.SetRefreshSchedule,
			LogLayers.ApplicationService,
			ApplicationLogOperations.UserRatings.SetRefreshSchedule,
			properties);

		_logger.LogStarted(
			DomainLogEvents.UserRatings.SetRefreshSchedule,
			LogLayers.ApplicationService,
			ApplicationLogOperations.UserRatings.SetRefreshSchedule,
			"User ratings refresh schedule update started.",
			properties);

		try
		{
			if(hour is < 0 or > 23 || minute is < 0 or > 59)
			{
				_logger.LogRejected(
					LogLevel.Warning,
					DomainLogEvents.UserRatings.SetRefreshSchedule,
					LogLayers.ApplicationService,
					ApplicationLogOperations.UserRatings.SetRefreshSchedule,
					"User ratings refresh schedule update rejected by validation.",
					StructuredLog.Combine(properties, ("Error", UserRatingsAdminError.ValidationFailed)));

				return UserRatingsAdminResult<UserRatingsRefreshScheduleModel>.Fail(UserRatingsAdminError.ValidationFailed);
			}

			var authError = await AuthorizeAdminAsync(actorUserId, actorUserPassword, cancellationToken);
			if(authError is not null)
			{
				_logger.LogRejected(
					LogLevel.Warning,
					DomainLogEvents.UserRatings.SetRefreshSchedule,
					LogLayers.ApplicationService,
					ApplicationLogOperations.UserRatings.SetRefreshSchedule,
					"User ratings refresh schedule update rejected.",
					StructuredLog.Combine(properties, ("Error", authError.Value)));

				return UserRatingsAdminResult<UserRatingsRefreshScheduleModel>.Fail(authError.Value);
			}

			var schedule = await _runtime.SetScheduleAsync(hour, minute, cancellationToken);

			_logger.LogCompleted(
				LogLevel.Information,
				DomainLogEvents.UserRatings.SetRefreshSchedule,
				LogLayers.ApplicationService,
				ApplicationLogOperations.UserRatings.SetRefreshSchedule,
				"User ratings refresh schedule update completed.",
				StructuredLog.Combine(properties, ("StoredHour", schedule.Hour), ("StoredMinute", schedule.Minute)));

			return UserRatingsAdminResult<UserRatingsRefreshScheduleModel>.Ok(schedule);
		}
		catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
		{
			_logger.LogAborted(
				LogLevel.Information,
				DomainLogEvents.UserRatings.SetRefreshSchedule,
				LogLayers.ApplicationService,
				ApplicationLogOperations.UserRatings.SetRefreshSchedule,
				"User ratings refresh schedule update aborted.",
				properties);
			throw;
		}
		catch(Exception ex)
		{
			_logger.LogFailed(
				LogLevel.Error,
				DomainLogEvents.UserRatings.SetRefreshSchedule,
				LogLayers.ApplicationService,
				ApplicationLogOperations.UserRatings.SetRefreshSchedule,
				"User ratings refresh schedule update failed.",
				ex,
				properties);
			throw;
		}
	}

	public async Task<UserRatingsAdminResult<UserRatingsRefreshRunModel>> RunRefreshNowAsync(
		Guid actorUserId,
		string actorUserPassword,
		CancellationToken cancellationToken)
	{
		var properties = new (string Name, object? Value)[]
		{
			("ActorUserId", actorUserId),
		};

		using var scope = _logger.BeginExecutionScope(
			DomainLogEvents.UserRatings.RunRefreshNow,
			LogLayers.ApplicationService,
			ApplicationLogOperations.UserRatings.RunRefreshNow,
			properties);

		_logger.LogStarted(
			DomainLogEvents.UserRatings.RunRefreshNow,
			LogLayers.ApplicationService,
			ApplicationLogOperations.UserRatings.RunRefreshNow,
			"User ratings refresh manual run started.",
			properties);

		try
		{
			var authError = await AuthorizeAdminAsync(actorUserId, actorUserPassword, cancellationToken);
			if(authError is not null)
			{
				_logger.LogRejected(
					LogLevel.Warning,
					DomainLogEvents.UserRatings.RunRefreshNow,
					LogLayers.ApplicationService,
					ApplicationLogOperations.UserRatings.RunRefreshNow,
					"User ratings refresh manual run rejected.",
					StructuredLog.Combine(properties, ("Error", authError.Value)));

				return UserRatingsAdminResult<UserRatingsRefreshRunModel>.Fail(authError.Value);
			}

			var result = await _runtime.RunNowAsync(cancellationToken);

			_logger.LogCompleted(
				LogLevel.Information,
				DomainLogEvents.UserRatings.RunRefreshNow,
				LogLayers.ApplicationService,
				ApplicationLogOperations.UserRatings.RunRefreshNow,
				"User ratings refresh manual run completed.",
				StructuredLog.Combine(
					properties,
					("StartedAtUtc", result.StartedAtUtc),
					("CompletedAtUtc", result.CompletedAtUtc),
					("TotalUsers", result.TotalUsers),
					("CreatedMissingRows", result.CreatedMissingRows),
					("UpdatedUsers", result.UpdatedUsers)));

			return UserRatingsAdminResult<UserRatingsRefreshRunModel>.Ok(result);
		}
		catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
		{
			_logger.LogAborted(
				LogLevel.Information,
				DomainLogEvents.UserRatings.RunRefreshNow,
				LogLayers.ApplicationService,
				ApplicationLogOperations.UserRatings.RunRefreshNow,
				"User ratings refresh manual run aborted.",
				properties);
			throw;
		}
		catch(Exception ex)
		{
			_logger.LogFailed(
				LogLevel.Error,
				DomainLogEvents.UserRatings.RunRefreshNow,
				LogLayers.ApplicationService,
				ApplicationLogOperations.UserRatings.RunRefreshNow,
				"User ratings refresh manual run failed.",
				ex,
				properties);
			throw;
		}
	}

	private async Task<UserRatingsAdminError?> AuthorizeAdminAsync(
		Guid actorUserId,
		string actorUserPassword,
		CancellationToken cancellationToken)
	{
		if(actorUserId == Guid.Empty || string.IsNullOrWhiteSpace(actorUserPassword))
		{
			return UserRatingsAdminError.ValidationFailed;
		}

		var auth = await _actorAccess.AuthenticateAsync(actorUserId, actorUserPassword, cancellationToken);
		if(!auth.IsSuccess)
		{
			return UserRatingsAdminError.InvalidCredentials;
		}

		return UserRoleRules.IsAdmin(auth.Actor!.Role)
			? null
			: UserRatingsAdminError.Forbidden;
	}
}