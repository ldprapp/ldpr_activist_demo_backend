using System.Globalization;

using LdprActivistDemo.Application.Diagnostics;
using LdprActivistDemo.Application.Logging;
using LdprActivistDemo.Application.Referrals.Models;
using LdprActivistDemo.Application.Users;
using LdprActivistDemo.Application.Users.Models;

using Microsoft.Extensions.Logging;

namespace LdprActivistDemo.Application.Referrals;

public sealed class ReferralService : IReferralService
{
	private readonly IUserService _users;
	private readonly IUserRepository _userRepository;
	private readonly IReferralSettingsRepository _referralSettings;
	private readonly IActorAccessService _actorAccess;
	private readonly ILogger<ReferralService> _logger;

	public ReferralService(
		IUserService users,
		IUserRepository userRepository,
		IReferralSettingsRepository referralSettings,
		IActorAccessService actorAccess,
		ILogger<ReferralService> logger)
	{
		_users = users ?? throw new ArgumentNullException(nameof(users));
		_userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
		_referralSettings = referralSettings ?? throw new ArgumentNullException(nameof(referralSettings));
		_actorAccess = actorAccess ?? throw new ArgumentNullException(nameof(actorAccess));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	public async Task<ReferralContentResult> GetContentAsync(
		Guid actorUserId,
		string actorUserPassword,
		Guid userId,
		ReferralContentFormat format,
		CancellationToken cancellationToken)
	{
		var properties = new (string Name, object? Value)[]
		{
			("ActorUserId", actorUserId),
			("TargetUserId", userId),
			("Format", format.ToString()),
		};

		using var scope = _logger.BeginExecutionScope(
			DomainLogEvents.Referral.GetContent,
			LogLayers.ApplicationService,
			ApplicationLogOperations.Referrals.GetContent,
			properties);

		_logger.LogStarted(
			DomainLogEvents.Referral.GetContent,
			LogLayers.ApplicationService,
			ApplicationLogOperations.Referrals.GetContent,
			"Referral content read started.",
			properties);

		try
		{
			if(actorUserId == Guid.Empty || userId == Guid.Empty || string.IsNullOrWhiteSpace(actorUserPassword))
			{
				_logger.LogRejected(
					LogLevel.Warning,
					DomainLogEvents.Referral.GetContent,
					LogLayers.ApplicationService,
					ApplicationLogOperations.Referrals.GetContent,
					"Referral content read rejected by validation.",
					StructuredLog.Combine(properties, ("Error", ReferralContentError.ValidationFailed)));

				return ReferralContentResult.Fail(ReferralContentError.ValidationFailed);
			}

			var referralCodeResult = await _users.GetReferralCodeAsync(
				actorUserId,
				actorUserPassword,
				userId,
				cancellationToken);

			if(!referralCodeResult.IsSuccess)
			{
				var mappedError = MapReferralCodeError(referralCodeResult.Error);

				_logger.LogRejected(
					LogLevel.Warning,
					DomainLogEvents.Referral.GetContent,
					LogLayers.ApplicationService,
					ApplicationLogOperations.Referrals.GetContent,
					"Referral content read rejected.",
					StructuredLog.Combine(properties, ("Error", mappedError)));

				return ReferralContentResult.Fail(mappedError);
			}

			var referralCodeText = referralCodeResult.ReferralCode.ToString(CultureInfo.InvariantCulture);
			if(format == ReferralContentFormat.Code)
			{
				_logger.LogCompleted(
					LogLevel.Information,
					DomainLogEvents.Referral.GetContent,
					LogLayers.ApplicationService,
					ApplicationLogOperations.Referrals.GetContent,
					"Referral content read completed.",
					properties);

				return ReferralContentResult.Success(referralCodeText);
			}

			var inviteTextTemplate = await _referralSettings.GetInviteTextTemplateAsync(cancellationToken);
			var invitedUserRewardPoints = await _referralSettings.GetInvitedUserRewardPointsAsync(cancellationToken);
			var renderedText = inviteTextTemplate
				.Replace(
					ReferralDefaults.CodePlaceholder,
					referralCodeText,
					StringComparison.Ordinal)
				.Replace(
					ReferralDefaults.RewardPlaceholder,
					invitedUserRewardPoints.ToString(CultureInfo.InvariantCulture),
					StringComparison.Ordinal);

			_logger.LogCompleted(
				LogLevel.Information,
				DomainLogEvents.Referral.GetContent,
				LogLayers.ApplicationService,
				ApplicationLogOperations.Referrals.GetContent,
				"Referral content read completed.",
				properties);

			return ReferralContentResult.Success(renderedText);
		}
		catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
		{
			_logger.LogAborted(
				LogLevel.Information,
				DomainLogEvents.Referral.GetContent,
				LogLayers.ApplicationService,
				ApplicationLogOperations.Referrals.GetContent,
				"Referral content read aborted.",
				properties);
			throw;
		}
		catch(Exception ex)
		{
			_logger.LogFailed(
				LogLevel.Error,
				DomainLogEvents.Referral.GetContent,
				LogLayers.ApplicationService,
				ApplicationLogOperations.Referrals.GetContent,
				"Referral content read failed.",
				ex,
				properties);
			throw;
		}
	}

	public async Task<ReferralSettingsReadResult> GetSettingsAsync(
		CancellationToken cancellationToken)
	{
		var properties = Array.Empty<(string Name, object? Value)>();

		using var scope = _logger.BeginExecutionScope(
			DomainLogEvents.Referral.GetSettings,
		   LogLayers.ApplicationService,
		   ApplicationLogOperations.Referrals.GetSettings,
		   properties);

		_logger.LogStarted(
			DomainLogEvents.Referral.GetSettings,
			LogLayers.ApplicationService,
			ApplicationLogOperations.Referrals.GetSettings,
			"Referral settings read started.",
			properties);

		try
		{
			var inviteTextTemplate = await _referralSettings.GetInviteTextTemplateAsync(cancellationToken);
			var inviterRewardPoints = await _referralSettings.GetInviterRewardPointsAsync(cancellationToken);
			var invitedUserRewardPoints = await _referralSettings.GetInvitedUserRewardPointsAsync(cancellationToken);

			_logger.LogCompleted(
				LogLevel.Information,
				DomainLogEvents.Referral.GetSettings,
				LogLayers.ApplicationService,
				ApplicationLogOperations.Referrals.GetSettings,
				"Referral settings read completed.",
				StructuredLog.Combine(
					properties,
					("InviterRewardPoints", inviterRewardPoints),
					("InvitedUserRewardPoints", invitedUserRewardPoints)));

			return ReferralSettingsReadResult.Success(
				inviteTextTemplate,
				inviterRewardPoints,
				invitedUserRewardPoints);
		}
		catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
		{
			_logger.LogAborted(
				LogLevel.Information,
				DomainLogEvents.Referral.GetSettings,
				LogLayers.ApplicationService,
				ApplicationLogOperations.Referrals.GetSettings,
				"Referral settings read aborted.",
				properties);
			throw;
		}
		catch(Exception ex)
		{
			_logger.LogFailed(
				LogLevel.Error,
				DomainLogEvents.Referral.GetSettings,
				LogLayers.ApplicationService,
				ApplicationLogOperations.Referrals.GetSettings,
				"Referral settings read failed.",
				ex,
				properties);
			throw;
		}
	}

	public async Task<ReferralInvitedUsersReadResult> GetInvitedUsersAsync(
		Guid actorUserId,
		string actorUserPassword,
		Guid userId,
		CancellationToken cancellationToken)
	{
		var properties = new (string Name, object? Value)[]
		{
			("ActorUserId", actorUserId),
			("TargetUserId", userId),
		};

		using var scope = _logger.BeginExecutionScope(
			DomainLogEvents.Referral.GetInvitedUsers,
			LogLayers.ApplicationService,
			ApplicationLogOperations.Referrals.GetInvitedUsers,
			properties);

		_logger.LogStarted(
			DomainLogEvents.Referral.GetInvitedUsers,
			LogLayers.ApplicationService,
			ApplicationLogOperations.Referrals.GetInvitedUsers,
			"Referral invited users read started.",
			properties);

		try
		{
			if(actorUserId == Guid.Empty || userId == Guid.Empty || string.IsNullOrWhiteSpace(actorUserPassword))
			{
				_logger.LogRejected(
					LogLevel.Warning,
					DomainLogEvents.Referral.GetInvitedUsers,
					LogLayers.ApplicationService,
					ApplicationLogOperations.Referrals.GetInvitedUsers,
					"Referral invited users read rejected by validation.",
					StructuredLog.Combine(properties, ("Error", ReferralInvitedUsersReadError.ValidationFailed)));

				return ReferralInvitedUsersReadResult.Fail(ReferralInvitedUsersReadError.ValidationFailed);
			}

			var actorAuth = await _actorAccess.AuthenticateAsync(actorUserId, actorUserPassword, cancellationToken);
			if(!actorAuth.IsSuccess)
			{
				var error = actorAuth.Error == ActorAuthenticationError.ValidationFailed
					? ReferralInvitedUsersReadError.ValidationFailed
					: ReferralInvitedUsersReadError.InvalidCredentials;

				_logger.LogRejected(
					LogLevel.Warning,
					DomainLogEvents.Referral.GetInvitedUsers,
					LogLayers.ApplicationService,
					ApplicationLogOperations.Referrals.GetInvitedUsers,
					"Referral invited users read rejected. Invalid actor credentials.",
					StructuredLog.Combine(properties, ("Error", error)));

				return ReferralInvitedUsersReadResult.Fail(error);
			}

			if(!UserRoleRules.IsAdmin(actorAuth.Actor!.Role) && actorUserId != userId)
			{
				_logger.LogRejected(
					LogLevel.Warning,
					DomainLogEvents.Referral.GetInvitedUsers,
					LogLayers.ApplicationService,
					ApplicationLogOperations.Referrals.GetInvitedUsers,
					"Referral invited users read rejected. Actor has no access to target user.",
					StructuredLog.Combine(properties, ("ActorRole", actorAuth.Actor.Role), ("Error", ReferralInvitedUsersReadError.Forbidden)));

				return ReferralInvitedUsersReadResult.Fail(ReferralInvitedUsersReadError.Forbidden);
			}

			var invitedUsers = await _userRepository.GetInvitedUsersAsync(userId, cancellationToken);
			if(invitedUsers is null)
			{
				_logger.LogRejected(
					LogLevel.Warning,
					DomainLogEvents.Referral.GetInvitedUsers,
					LogLayers.ApplicationService,
					ApplicationLogOperations.Referrals.GetInvitedUsers,
					"Referral invited users read rejected. Target user not found.",
					StructuredLog.Combine(properties, ("Error", ReferralInvitedUsersReadError.UserNotFound)));

				return ReferralInvitedUsersReadResult.Fail(ReferralInvitedUsersReadError.UserNotFound);
			}

			_logger.LogCompleted(
				LogLevel.Information,
				DomainLogEvents.Referral.GetInvitedUsers,
				LogLayers.ApplicationService,
				ApplicationLogOperations.Referrals.GetInvitedUsers,
				"Referral invited users read completed.",
				StructuredLog.Combine(
					properties,
					("ActorRole", actorAuth.Actor.Role),
					("Count", invitedUsers.Count)));

			return ReferralInvitedUsersReadResult.Success(invitedUsers);
		}
		catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
		{
			_logger.LogAborted(
				LogLevel.Information,
				DomainLogEvents.Referral.GetInvitedUsers,
				LogLayers.ApplicationService,
				ApplicationLogOperations.Referrals.GetInvitedUsers,
				"Referral invited users read aborted.",
				properties);
			throw;
		}
		catch(Exception ex)
		{
			_logger.LogFailed(
				LogLevel.Error,
				DomainLogEvents.Referral.GetInvitedUsers,
				LogLayers.ApplicationService,
				ApplicationLogOperations.Referrals.GetInvitedUsers,
				"Referral invited users read failed.",
				ex,
				properties);
			throw;
		}
	}

	public async Task<ReferralSettingsUpdateResult> UpdateSettingsAsync(
		Guid actorUserId,
		string actorUserPassword,
		string inviteTextTemplate,
		int inviterRewardPoints,
		int invitedUserRewardPoints,
		CancellationToken cancellationToken)
	{
		var normalizedInviteTextTemplate = NormalizeInviteTextTemplate(inviteTextTemplate);
		var properties = new (string Name, object? Value)[]
		{
			("ActorUserId", actorUserId),
			("InviterRewardPoints", inviterRewardPoints),
			("InvitedUserRewardPoints", invitedUserRewardPoints),
		};

		using var scope = _logger.BeginExecutionScope(
			DomainLogEvents.Referral.UpdateSettings,
			LogLayers.ApplicationService,
			ApplicationLogOperations.Referrals.UpdateSettings,
			properties);

		_logger.LogStarted(
			DomainLogEvents.Referral.UpdateSettings,
			LogLayers.ApplicationService,
			ApplicationLogOperations.Referrals.UpdateSettings,
			"Referral settings update started.",
			properties);

		try
		{
			if(actorUserId == Guid.Empty
			   || string.IsNullOrWhiteSpace(actorUserPassword)
			   || normalizedInviteTextTemplate is null
			   || !normalizedInviteTextTemplate.Contains(ReferralDefaults.CodePlaceholder, StringComparison.Ordinal)
			   || inviterRewardPoints < 0
			   || invitedUserRewardPoints < 0)
			{
				_logger.LogRejected(
					LogLevel.Warning,
					DomainLogEvents.Referral.UpdateSettings,
					LogLayers.ApplicationService,
					ApplicationLogOperations.Referrals.UpdateSettings,
					"Referral settings update rejected by validation.",
					StructuredLog.Combine(properties, ("Error", ReferralSettingsUpdateError.ValidationFailed)));

				return ReferralSettingsUpdateResult.Fail(ReferralSettingsUpdateError.ValidationFailed);
			}

			var actorAuth = await _actorAccess.AuthenticateAsync(actorUserId, actorUserPassword, cancellationToken);
			if(!actorAuth.IsSuccess)
			{
				var error = actorAuth.Error == ActorAuthenticationError.ValidationFailed
					? ReferralSettingsUpdateError.ValidationFailed
					: ReferralSettingsUpdateError.InvalidCredentials;

				_logger.LogRejected(
					LogLevel.Warning,
					DomainLogEvents.Referral.UpdateSettings,
					LogLayers.ApplicationService,
					ApplicationLogOperations.Referrals.UpdateSettings,
					"Referral settings update rejected. Invalid actor credentials.",
					StructuredLog.Combine(properties, ("Error", error)));

				return ReferralSettingsUpdateResult.Fail(error);
			}

			if(!UserRoleRules.IsAdmin(actorAuth.Actor!.Role))
			{
				_logger.LogRejected(
					LogLevel.Warning,
					DomainLogEvents.Referral.UpdateSettings,
					LogLayers.ApplicationService,
					ApplicationLogOperations.Referrals.UpdateSettings,
					"Referral settings update rejected. Actor is not admin.",
					StructuredLog.Combine(properties, ("ActorRole", actorAuth.Actor.Role), ("Error", ReferralSettingsUpdateError.Forbidden)));

				return ReferralSettingsUpdateResult.Fail(ReferralSettingsUpdateError.Forbidden);
			}

			await _referralSettings.SetSettingsAsync(
				normalizedInviteTextTemplate,
				inviterRewardPoints,
				invitedUserRewardPoints,
				cancellationToken);

			_logger.LogCompleted(
				LogLevel.Information,
				DomainLogEvents.Referral.UpdateSettings,
				LogLayers.ApplicationService,
				ApplicationLogOperations.Referrals.UpdateSettings,
				"Referral settings update completed.",
				StructuredLog.Combine(properties, ("ActorRole", actorAuth.Actor.Role)));

			return ReferralSettingsUpdateResult.Success(
				normalizedInviteTextTemplate,
				inviterRewardPoints,
				invitedUserRewardPoints);
		}
		catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
		{
			_logger.LogAborted(
				LogLevel.Information,
				DomainLogEvents.Referral.UpdateSettings,
				LogLayers.ApplicationService,
				ApplicationLogOperations.Referrals.UpdateSettings,
				"Referral settings update aborted.",
				properties);
			throw;
		}
		catch(Exception ex)
		{
			_logger.LogFailed(
				LogLevel.Error,
				DomainLogEvents.Referral.UpdateSettings,
				LogLayers.ApplicationService,
				ApplicationLogOperations.Referrals.UpdateSettings,
				"Referral settings update failed.",
				ex,
				properties);
			throw;
		}
	}

	public async Task<ReferralTemplateUpdateResult> SetInviteTextTemplateAsync(
		Guid actorUserId,
		string actorUserPassword,
		string inviteTextTemplate,
		CancellationToken cancellationToken)
	{
		var normalizedInviteTextTemplate = NormalizeInviteTextTemplate(inviteTextTemplate);
		var properties = new (string Name, object? Value)[]
		{
			("ActorUserId", actorUserId),
		};

		using var scope = _logger.BeginExecutionScope(
			DomainLogEvents.Referral.SetInviteTextTemplate,
			LogLayers.ApplicationService,
			ApplicationLogOperations.Referrals.SetInviteTextTemplate,
			properties);

		_logger.LogStarted(
			DomainLogEvents.Referral.SetInviteTextTemplate,
			LogLayers.ApplicationService,
			ApplicationLogOperations.Referrals.SetInviteTextTemplate,
			"Referral invite text template update started.",
			properties);

		try
		{
			if(actorUserId == Guid.Empty
			   || string.IsNullOrWhiteSpace(actorUserPassword)
			   || normalizedInviteTextTemplate is null
			   || !normalizedInviteTextTemplate.Contains(ReferralDefaults.CodePlaceholder, StringComparison.Ordinal))
			{
				_logger.LogRejected(
					LogLevel.Warning,
					DomainLogEvents.Referral.SetInviteTextTemplate,
					LogLayers.ApplicationService,
					ApplicationLogOperations.Referrals.SetInviteTextTemplate,
					"Referral invite text template update rejected by validation.",
					StructuredLog.Combine(properties, ("Error", ReferralTemplateUpdateError.ValidationFailed)));

				return ReferralTemplateUpdateResult.Fail(ReferralTemplateUpdateError.ValidationFailed);
			}

			var actorAuth = await _actorAccess.AuthenticateAsync(actorUserId, actorUserPassword, cancellationToken);
			if(!actorAuth.IsSuccess)
			{
				var error = actorAuth.Error == ActorAuthenticationError.ValidationFailed
					? ReferralTemplateUpdateError.ValidationFailed
					: ReferralTemplateUpdateError.InvalidCredentials;

				_logger.LogRejected(
					LogLevel.Warning,
					DomainLogEvents.Referral.SetInviteTextTemplate,
					LogLayers.ApplicationService,
					ApplicationLogOperations.Referrals.SetInviteTextTemplate,
					"Referral invite text template update rejected. Invalid actor credentials.",
					StructuredLog.Combine(properties, ("Error", error)));

				return ReferralTemplateUpdateResult.Fail(error);
			}

			if(!UserRoleRules.IsAdmin(actorAuth.Actor!.Role))
			{
				_logger.LogRejected(
					LogLevel.Warning,
					DomainLogEvents.Referral.SetInviteTextTemplate,
					LogLayers.ApplicationService,
					ApplicationLogOperations.Referrals.SetInviteTextTemplate,
					"Referral invite text template update rejected. Actor is not admin.",
					StructuredLog.Combine(properties, ("ActorRole", actorAuth.Actor.Role), ("Error", ReferralTemplateUpdateError.Forbidden)));

				return ReferralTemplateUpdateResult.Fail(ReferralTemplateUpdateError.Forbidden);
			}

			await _referralSettings.SetInviteTextTemplateAsync(normalizedInviteTextTemplate, cancellationToken);

			_logger.LogCompleted(
				LogLevel.Information,
				DomainLogEvents.Referral.SetInviteTextTemplate,
				LogLayers.ApplicationService,
				ApplicationLogOperations.Referrals.SetInviteTextTemplate,
				"Referral invite text template update completed.",
				StructuredLog.Combine(properties, ("ActorRole", actorAuth.Actor.Role)));

			return ReferralTemplateUpdateResult.Success(normalizedInviteTextTemplate);
		}
		catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
		{
			_logger.LogAborted(
				LogLevel.Information,
				DomainLogEvents.Referral.SetInviteTextTemplate,
				LogLayers.ApplicationService,
				ApplicationLogOperations.Referrals.SetInviteTextTemplate,
				"Referral invite text template update aborted.",
				properties);
			throw;
		}
		catch(Exception ex)
		{
			_logger.LogFailed(
				LogLevel.Error,
				DomainLogEvents.Referral.SetInviteTextTemplate,
				LogLayers.ApplicationService,
				ApplicationLogOperations.Referrals.SetInviteTextTemplate,
				"Referral invite text template update failed.",
				ex,
				properties);
			throw;
		}
	}

	private static ReferralContentError MapReferralCodeError(UserReferralCodeError error)
		=> error switch
		{
			UserReferralCodeError.ValidationFailed => ReferralContentError.ValidationFailed,
			UserReferralCodeError.InvalidCredentials => ReferralContentError.InvalidCredentials,
			UserReferralCodeError.Forbidden => ReferralContentError.Forbidden,
			UserReferralCodeError.UserNotFound => ReferralContentError.UserNotFound,
			_ => ReferralContentError.ValidationFailed,
		};

	private static string? NormalizeInviteTextTemplate(string? value)
	{
		var normalized = (value ?? string.Empty).Trim();
		return normalized.Length == 0 ? null : normalized;
	}
}