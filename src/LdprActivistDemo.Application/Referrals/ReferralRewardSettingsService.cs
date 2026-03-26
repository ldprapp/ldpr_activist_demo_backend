using LdprActivistDemo.Application.Diagnostics;
using LdprActivistDemo.Application.Logging;
using LdprActivistDemo.Application.Users;
using LdprActivistDemo.Application.Users.Models;

using Microsoft.Extensions.Logging;

namespace LdprActivistDemo.Application.Referrals;

/// <summary>
/// Сервис административного изменения сумм реферальных бонусов.
/// </summary>
public sealed class ReferralRewardSettingsService : IReferralRewardSettingsService
{
	private readonly IActorAccessService _actorAccess;
	private readonly IReferralRewardSettingsRepository _settings;
	private readonly ILogger<ReferralRewardSettingsService> _logger;

	public ReferralRewardSettingsService(
		IActorAccessService actorAccess,
		IReferralRewardSettingsRepository settings,
		ILogger<ReferralRewardSettingsService> logger)
	{
		_actorAccess = actorAccess ?? throw new ArgumentNullException(nameof(actorAccess));
		_settings = settings ?? throw new ArgumentNullException(nameof(settings));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	public Task<ReferralRewardSettingsChangeResult> SetInviterRewardPointsAsync(
		Guid actorUserId,
		string actorUserPassword,
		int points,
		CancellationToken cancellationToken)
		=> ExecuteChangeAsync(
			DomainLogEvents.Referral.SetInviterRewardPoints,
			ApplicationLogOperations.Referrals.SetInviterRewardPoints,
			actorUserId,
			actorUserPassword,
			points,
			() => _settings.SetInviterRewardPointsAsync(points, cancellationToken),
			cancellationToken,
			("InviterRewardPoints", points));

	public Task<ReferralRewardSettingsChangeResult> SetInvitedUserRewardPointsAsync(
		Guid actorUserId,
		string actorUserPassword,
		int points,
		CancellationToken cancellationToken)
		=> ExecuteChangeAsync(
			DomainLogEvents.Referral.SetInvitedUserRewardPoints,
			ApplicationLogOperations.Referrals.SetInvitedUserRewardPoints,
			actorUserId,
			actorUserPassword,
			points,
			() => _settings.SetInvitedUserRewardPointsAsync(points, cancellationToken),
			cancellationToken,
			("InvitedUserRewardPoints", points));

	private async Task<ReferralRewardSettingsChangeResult> ExecuteChangeAsync(
		string eventName,
		string operationName,
		Guid actorUserId,
		string actorUserPassword,
		int points,
		Func<Task> applyAsync,
		CancellationToken cancellationToken,
		params (string Name, object? Value)[] operationProperties)
	{
		var properties = StructuredLog.Combine(
			operationProperties,
			("ActorUserId", actorUserId));

		using var scope = _logger.BeginExecutionScope(
			eventName,
			LogLayers.ApplicationService,
			operationName,
			properties);

		_logger.LogStarted(
			eventName,
			LogLayers.ApplicationService,
			operationName,
			"Referral reward settings change started.",
			properties);

		try
		{
			if(actorUserId == Guid.Empty || string.IsNullOrWhiteSpace(actorUserPassword) || points < 0)
			{
				_logger.LogRejected(
					LogLevel.Warning,
					eventName,
					LogLayers.ApplicationService,
					operationName,
					"Referral reward settings change rejected by validation.",
					StructuredLog.Combine(properties, ("Error", ReferralRewardSettingsChangeError.ValidationFailed)));

				return ReferralRewardSettingsChangeResult.Fail(ReferralRewardSettingsChangeError.ValidationFailed);
			}

			var actorAuth = await _actorAccess.AuthenticateAsync(actorUserId, actorUserPassword, cancellationToken);
			if(!actorAuth.IsSuccess)
			{
				var error = actorAuth.Error == ActorAuthenticationError.ValidationFailed
					? ReferralRewardSettingsChangeError.ValidationFailed
					: ReferralRewardSettingsChangeError.InvalidCredentials;

				_logger.LogRejected(
					LogLevel.Warning,
					eventName,
					LogLayers.ApplicationService,
					operationName,
					"Referral reward settings change rejected. Invalid actor credentials.",
					StructuredLog.Combine(properties, ("Error", error)));

				return ReferralRewardSettingsChangeResult.Fail(error);
			}

			if(!UserRoleRules.IsAdmin(actorAuth.Actor!.Role))
			{
				_logger.LogRejected(
					LogLevel.Warning,
					eventName,
					LogLayers.ApplicationService,
					operationName,
					"Referral reward settings change rejected. Actor is not admin.",
					StructuredLog.Combine(
						properties,
						("ActorRole", actorAuth.Actor.Role),
						("Error", ReferralRewardSettingsChangeError.Forbidden)));

				return ReferralRewardSettingsChangeResult.Fail(ReferralRewardSettingsChangeError.Forbidden);
			}

			await applyAsync();

			_logger.LogCompleted(
				LogLevel.Information,
				eventName,
				LogLayers.ApplicationService,
				operationName,
				"Referral reward settings change completed.",
				StructuredLog.Combine(properties, ("ActorRole", actorAuth.Actor.Role)));

			return ReferralRewardSettingsChangeResult.Success();
		}
		catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
		{
			_logger.LogAborted(
				LogLevel.Information,
				eventName,
				LogLayers.ApplicationService,
				operationName,
				"Referral reward settings change aborted.",
				properties);
			throw;
		}
		catch(Exception ex)
		{
			_logger.LogFailed(
				LogLevel.Error,
				eventName,
				LogLayers.ApplicationService,
				operationName,
				"Referral reward settings change failed.",
				ex,
				properties);
			throw;
		}
	}
}