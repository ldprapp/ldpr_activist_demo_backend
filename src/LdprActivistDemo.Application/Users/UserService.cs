using LdprActivistDemo.Application.Diagnostics;
using LdprActivistDemo.Application.Logging;
using LdprActivistDemo.Application.Otp;
using LdprActivistDemo.Application.Users.Models;

using Microsoft.Extensions.Logging;

namespace LdprActivistDemo.Application.Users;

public sealed class UserService : IUserService
{
	private readonly IUserRepository _users;
	private readonly IOtpService _otp;
	private readonly IUnconfirmedUserCleanupScheduler _cleanupScheduler;
	private readonly IActorAccessService _actorAccess;
	private readonly ILogger<UserService> _logger;

	public UserService(
		IUserRepository users,
		IOtpService otp,
		IUnconfirmedUserCleanupScheduler cleanupScheduler,
		IActorAccessService actorAccess,
		ILogger<UserService> logger)
	{
		_users = users ?? throw new ArgumentNullException(nameof(users));
		_otp = otp ?? throw new ArgumentNullException(nameof(otp));
		_cleanupScheduler = cleanupScheduler ?? throw new ArgumentNullException(nameof(cleanupScheduler));
		_actorAccess = actorAccess ?? throw new ArgumentNullException(nameof(actorAccess));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	public async Task<Guid> RegisterAsync(UserCreateModel model, CancellationToken cancellationToken)
	{
		var maskedPhoneNumber = MaskPhoneNumber(model.PhoneNumber);
		var properties = new (string Name, object? Value)[]
		{
			("PhoneNumber", maskedPhoneNumber),
			("RegionName", model.RegionName),
			("SettlementName", model.SettlementName),
		};

		using var scope = _logger.BeginExecutionScope(
			DomainLogEvents.User.Register,
			LogLayers.ApplicationService,
			ApplicationLogOperations.Users.Register,
			properties);

		_logger.LogStarted(
			DomainLogEvents.User.Register,
			LogLayers.ApplicationService,
			ApplicationLogOperations.Users.Register,
			"User registration started.",
			properties);

		try
		{
			var confirmedExists = await _users.ExistsConfirmedByPhoneAsync(model.PhoneNumber, cancellationToken);
			if(confirmedExists)
			{
				_logger.LogRejected(
					LogLevel.Warning,
					DomainLogEvents.User.Register,
					LogLayers.ApplicationService,
					ApplicationLogOperations.Users.Register,
					"User registration rejected. Confirmed phone already exists.",
					StructuredLog.Combine(properties, ("RejectedReason", "PhoneAlreadyExists")));
				throw new InvalidOperationException("PhoneNumber already exists.");
			}

			await _users.DeleteUnconfirmedByPhoneAsync(model.PhoneNumber, cancellationToken);
			var userId = await _users.CreateAsync(model, cancellationToken);
			_cleanupScheduler.Schedule(userId);

			_logger.LogCompleted(
				LogLevel.Information,
				DomainLogEvents.User.Register,
				LogLayers.ApplicationService,
				ApplicationLogOperations.Users.Register,
				"User registration completed.",
				StructuredLog.Combine(properties, ("UserId", userId)));

			return userId;
		}
		catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
		{
			_logger.LogAborted(
				LogLevel.Information,
				DomainLogEvents.User.Register,
				LogLayers.ApplicationService,
				ApplicationLogOperations.Users.Register,
				"User registration aborted.",
				properties);
			throw;
		}
		catch(Exception ex)
		{
			_logger.LogFailed(
				LogLevel.Error,
				DomainLogEvents.User.Register,
				LogLayers.ApplicationService,
				ApplicationLogOperations.Users.Register,
				"User registration failed.",
				ex,
				properties);
			throw;
		}
	}

	public async Task<bool> ConfirmPhoneAsync(string phoneNumber, string otpCode, CancellationToken cancellationToken)
	{
		var properties = new (string Name, object? Value)[]
		{
			("PhoneNumber", MaskPhoneNumber(phoneNumber)),
		};

		using var scope = _logger.BeginExecutionScope(
			DomainLogEvents.User.ConfirmPhone,
			LogLayers.ApplicationService,
			ApplicationLogOperations.Users.ConfirmPhone,
			properties);

		_logger.LogStarted(
			DomainLogEvents.User.ConfirmPhone,
			LogLayers.ApplicationService,
			ApplicationLogOperations.Users.ConfirmPhone,
			"Phone confirmation started.",
			properties);

		try
		{
			var ok = await _otp.VerifyAsync(phoneNumber, otpCode, cancellationToken);
			if(!ok)
			{
				_logger.LogRejected(
					LogLevel.Warning,
					DomainLogEvents.User.ConfirmPhone,
					LogLayers.ApplicationService,
					ApplicationLogOperations.Users.ConfirmPhone,
					"Phone confirmation rejected. Invalid OTP.",
					StructuredLog.Combine(properties, ("RejectedReason", "OtpInvalid")));
				return false;
			}

			var confirmed = await _users.SetPhoneConfirmedAsync(phoneNumber, true, cancellationToken);
			if(!confirmed)
			{
				_logger.LogRejected(
					LogLevel.Warning,
					DomainLogEvents.User.ConfirmPhone,
					LogLayers.ApplicationService,
					ApplicationLogOperations.Users.ConfirmPhone,
					"Phone confirmation rejected. User not found.",
					StructuredLog.Combine(properties, ("RejectedReason", "UserNotFound")));
				return false;
			}

			_logger.LogCompleted(
				LogLevel.Information,
				DomainLogEvents.User.ConfirmPhone,
				LogLayers.ApplicationService,
				ApplicationLogOperations.Users.ConfirmPhone,
				"Phone confirmation completed.",
				properties);

			return true;
		}
		catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
		{
			_logger.LogAborted(
				LogLevel.Information,
				DomainLogEvents.User.ConfirmPhone,
				LogLayers.ApplicationService,
				ApplicationLogOperations.Users.ConfirmPhone,
				"Phone confirmation aborted.",
				properties);
			throw;
		}
		catch(Exception ex)
		{
			_logger.LogFailed(
				LogLevel.Error,
				DomainLogEvents.User.ConfirmPhone,
				LogLayers.ApplicationService,
				ApplicationLogOperations.Users.ConfirmPhone,
				"Phone confirmation failed.",
				ex,
				properties);
			throw;
		}
	}

	public async Task<LoginResult> LoginAsync(string phoneNumber, string password, CancellationToken cancellationToken)
	{
		var properties = new (string Name, object? Value)[]
		{
			("PhoneNumber", MaskPhoneNumber(phoneNumber)),
		}; using var scope = _logger.BeginExecutionScope(
			DomainLogEvents.User.Login,
			LogLayers.ApplicationService,
			ApplicationLogOperations.Users.Login,
			properties);
		_logger.LogStarted(
			DomainLogEvents.User.Login,
			LogLayers.ApplicationService,
			ApplicationLogOperations.Users.Login,
			"User login started.",
			properties);

		try
		{
			var u = await _users.GetInternalByPhoneAsync(phoneNumber, cancellationToken);
			if(u is null)
			{
				_logger.LogRejected(
					LogLevel.Warning,
					DomainLogEvents.User.Login,
					LogLayers.ApplicationService,
					ApplicationLogOperations.Users.Login,
					"User login rejected. User not found.",
					StructuredLog.Combine(properties, ("Error", LoginError.InvalidCredentials)));
				return LoginResult.Fail(LoginError.InvalidCredentials);
			}

			if(UserRoleRules.IsBanned(u.Role))
			{
				_logger.LogRejected(
					LogLevel.Warning,
					DomainLogEvents.User.Login,
					LogLayers.ApplicationService,
					ApplicationLogOperations.Users.Login,
					"User login rejected. User is banned.",
					StructuredLog.Combine(properties, ("UserId", u.Id), ("Error", LoginError.UserNotFound)));
				return LoginResult.Fail(LoginError.UserNotFound);
			}

			var passwordOk = await _users.ValidatePasswordAsync(phoneNumber, password, cancellationToken);
			if(!passwordOk)
			{
				_logger.LogRejected(
					LogLevel.Warning,
					DomainLogEvents.User.Login,
					LogLayers.ApplicationService,
					ApplicationLogOperations.Users.Login,
					"User login rejected. User not found after password validation.",
					StructuredLog.Combine(properties, ("Error", LoginError.InvalidCredentials)));
				return LoginResult.Fail(LoginError.InvalidCredentials);
			}

			if(!u.IsPhoneConfirmed)
			{
				_logger.LogRejected(
					LogLevel.Warning,
					DomainLogEvents.User.Login,
					LogLayers.ApplicationService,
					ApplicationLogOperations.Users.Login,
					"User login rejected. Phone is not confirmed.",
					StructuredLog.Combine(properties, ("UserId", u.Id), ("Error", LoginError.PhoneNotConfirmed)));
				return LoginResult.Fail(LoginError.PhoneNotConfirmed);
			}

			_logger.LogCompleted(
				LogLevel.Information,
				DomainLogEvents.User.Login,
				LogLayers.ApplicationService,
				ApplicationLogOperations.Users.Login,
				"User login completed.",
				StructuredLog.Combine(properties, ("UserId", u.Id), ("Role", u.Role)));

			return LoginResult.Ok(ToPublic(u));
		}
		catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
		{
			_logger.LogAborted(
				LogLevel.Information,
				DomainLogEvents.User.Login,
				LogLayers.ApplicationService,
				ApplicationLogOperations.Users.Login,
				"User login aborted.",
				properties);
			throw;
		}
		catch(Exception ex)
		{
			_logger.LogFailed(
				LogLevel.Error,
				DomainLogEvents.User.Login,
				LogLayers.ApplicationService,
				ApplicationLogOperations.Users.Login,
				"User login failed.",
				ex,
				properties);
			throw;
		}
	}

	public Task<UserPublicModel?> GetByPhoneAsync(string phoneNumber, CancellationToken cancellationToken)
		=> ExecuteReadAsync(
			DomainLogEvents.User.GetByPhone,
			ApplicationLogOperations.Users.GetByPhone,
			() => _users.GetPublicByPhoneAsync(phoneNumber, cancellationToken),
			cancellationToken,
			result => new (string Name, object? Value)[]
			{
				("Found", result is not null),
			},
			("PhoneNumber", MaskPhoneNumber(phoneNumber)));

	public Task<UserPublicModel?> GetByIdAsync(Guid userId, CancellationToken cancellationToken)
		=> ExecuteReadAsync(
			DomainLogEvents.User.GetById,
			ApplicationLogOperations.Users.GetById,
			() => _users.GetPublicByIdAsync(userId, cancellationToken),
			cancellationToken,
			result => new (string Name, object? Value)[]
			{
				("Found", result is not null),
			},
			("UserId", userId));

	public async Task<bool> ChangePasswordAsync(Guid userId, string newPassword, CancellationToken cancellationToken)
	{
		var properties = new (string Name, object? Value)[]
		{
			("UserId", userId),
		};

		using var scope = _logger.BeginExecutionScope(
			DomainLogEvents.User.ChangePassword,
			LogLayers.ApplicationService,
			ApplicationLogOperations.Users.ChangePassword,
			properties);

		_logger.LogStarted(
			DomainLogEvents.User.ChangePassword,
			LogLayers.ApplicationService,
			ApplicationLogOperations.Users.ChangePassword,
			"User password change started.",
			properties);

		try
		{
			var changed = await _users.SetPasswordAsync(userId, newPassword, cancellationToken);
			if(!changed)
			{
				_logger.LogRejected(
					LogLevel.Warning,
					DomainLogEvents.User.ChangePassword,
					LogLayers.ApplicationService,
					ApplicationLogOperations.Users.ChangePassword,
					"User password change rejected. User not found.",
					StructuredLog.Combine(properties, ("RejectedReason", "UserNotFound")));
				return false;
			}

			_logger.LogCompleted(
				LogLevel.Information,
				DomainLogEvents.User.ChangePassword,
				LogLayers.ApplicationService,
				ApplicationLogOperations.Users.ChangePassword,
				"User password change completed.",
				properties);
			return true;
		}
		catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
		{
			_logger.LogAborted(
				LogLevel.Information,
				DomainLogEvents.User.ChangePassword,
				LogLayers.ApplicationService,
				ApplicationLogOperations.Users.ChangePassword,
				"User password change aborted.",
				properties);
			throw;
		}
		catch(Exception ex)
		{
			_logger.LogFailed(
				LogLevel.Error,
				DomainLogEvents.User.ChangePassword,
				LogLayers.ApplicationService,
				ApplicationLogOperations.Users.ChangePassword,
				"User password change failed.",
				ex,
				properties);
			throw;
		}
	}

	public async Task<bool> SetAvatarImageAsync(Guid userId, Guid? avatarImageId, CancellationToken cancellationToken)
	{
		var properties = new (string Name, object? Value)[]
		{
			("UserId", userId),
			("AvatarImageId", avatarImageId),
		};

		using var scope = _logger.BeginExecutionScope(
			DomainLogEvents.User.SetAvatar,
			LogLayers.ApplicationService,
			ApplicationLogOperations.Users.SetAvatar,
			properties);

		_logger.LogStarted(
			DomainLogEvents.User.SetAvatar,
			LogLayers.ApplicationService,
			ApplicationLogOperations.Users.SetAvatar,
			"User avatar update started.",
			properties);

		try
		{
			var changed = await _users.SetAvatarImageAsync(userId, avatarImageId, cancellationToken);
			if(!changed)
			{
				_logger.LogRejected(
					LogLevel.Warning,
					DomainLogEvents.User.SetAvatar,
					LogLayers.ApplicationService,
					ApplicationLogOperations.Users.SetAvatar,
					"User avatar update rejected. User not found.",
					StructuredLog.Combine(properties, ("RejectedReason", "UserNotFound")));
				return false;
			}

			_logger.LogCompleted(
				LogLevel.Information,
				DomainLogEvents.User.SetAvatar,
				LogLayers.ApplicationService,
				ApplicationLogOperations.Users.SetAvatar,
				"User avatar update completed.",
				properties);
			return true;
		}
		catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
		{
			_logger.LogAborted(
				LogLevel.Information,
				DomainLogEvents.User.SetAvatar,
				LogLayers.ApplicationService,
				ApplicationLogOperations.Users.SetAvatar,
				"User avatar update aborted.",
				properties);
			throw;
		}
		catch(Exception ex)
		{
			_logger.LogFailed(
				LogLevel.Error,
				DomainLogEvents.User.SetAvatar,
				LogLayers.ApplicationService,
				ApplicationLogOperations.Users.SetAvatar,
				"User avatar update failed.",
				ex,
				properties);
			throw;
		}
	}

	public async Task<bool> UpdateAsync(UserUpdateModel model, CancellationToken cancellationToken)
	{
		var properties = new (string Name, object? Value)[]
		{
			("UserId", model.UserId),
			("RegionName", model.RegionName),
			("SettlementName", model.SettlementName),
		};

		using var scope = _logger.BeginExecutionScope(
			DomainLogEvents.User.UpdateProfile,
			LogLayers.ApplicationService,
			ApplicationLogOperations.Users.UpdateProfile,
			properties);

		_logger.LogStarted(
			DomainLogEvents.User.UpdateProfile,
			LogLayers.ApplicationService,
			ApplicationLogOperations.Users.UpdateProfile,
			"User profile update started.",
			properties);

		try
		{
			var changed = await _users.UpdateAsync(model, cancellationToken);
			if(!changed)
			{
				_logger.LogRejected(
					LogLevel.Warning,
					DomainLogEvents.User.UpdateProfile,
					LogLayers.ApplicationService,
					ApplicationLogOperations.Users.UpdateProfile,
					"User profile update rejected. User not found.",
					StructuredLog.Combine(properties, ("RejectedReason", "UserNotFound")));
				return false;
			}

			_logger.LogCompleted(
				LogLevel.Information,
				DomainLogEvents.User.UpdateProfile,
				LogLayers.ApplicationService,
				ApplicationLogOperations.Users.UpdateProfile,
				"User profile update completed.",
				properties);
			return true;
		}
		catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
		{
			_logger.LogAborted(
				LogLevel.Information,
				DomainLogEvents.User.UpdateProfile,
				LogLayers.ApplicationService,
				ApplicationLogOperations.Users.UpdateProfile,
				"User profile update aborted.",
				properties);
			throw;
		}
		catch(Exception ex)
		{
			_logger.LogFailed(
				LogLevel.Error,
				DomainLogEvents.User.UpdateProfile,
				LogLayers.ApplicationService,
				ApplicationLogOperations.Users.UpdateProfile,
				"User profile update failed.",
				ex,
				properties);
			throw;
		}
	}

	public async Task<bool> ChangePhoneAsync(Guid userId, string newPhoneNumber, string otpCode, CancellationToken cancellationToken)
	{
		var properties = new (string Name, object? Value)[]
		{
			("UserId", userId),
			("NewPhoneNumber", MaskPhoneNumber(newPhoneNumber)),
		};

		using var scope = _logger.BeginExecutionScope(
			DomainLogEvents.User.ChangePhone,
			LogLayers.ApplicationService,
			ApplicationLogOperations.Users.ChangePhone,
			properties);

		_logger.LogStarted(
			DomainLogEvents.User.ChangePhone,
			LogLayers.ApplicationService,
			ApplicationLogOperations.Users.ChangePhone,
			"User phone change started.",
			properties);

		try
		{
			var otpOk = await _otp.VerifyAsync(newPhoneNumber, otpCode, cancellationToken);
			if(!otpOk)
			{
				_logger.LogRejected(
					LogLevel.Warning,
					DomainLogEvents.User.ChangePhone,
					LogLayers.ApplicationService,
					ApplicationLogOperations.Users.ChangePhone,
					"User phone change rejected. Invalid OTP.",
					StructuredLog.Combine(properties, ("RejectedReason", "OtpInvalid")));
				return false;
			}

			var changed = await _users.ChangePhoneAsync(userId, newPhoneNumber, cancellationToken);
			if(!changed)
			{
				_logger.LogRejected(
					LogLevel.Warning,
					DomainLogEvents.User.ChangePhone,
					LogLayers.ApplicationService,
					ApplicationLogOperations.Users.ChangePhone,
					"User phone change rejected. User not found.",
					StructuredLog.Combine(properties, ("RejectedReason", "UserNotFound")));
				return false;
			}

			await _users.SetPhoneConfirmedAsync(newPhoneNumber, true, cancellationToken);

			_logger.LogCompleted(
				LogLevel.Information,
				DomainLogEvents.User.ChangePhone,
				LogLayers.ApplicationService,
				ApplicationLogOperations.Users.ChangePhone,
				"User phone change completed.",
				properties);

			return true;
		}
		catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
		{
			_logger.LogAborted(
				LogLevel.Information,
				DomainLogEvents.User.ChangePhone,
				LogLayers.ApplicationService,
				ApplicationLogOperations.Users.ChangePhone,
				"User phone change aborted.",
				properties);
			throw;
		}
		catch(Exception ex)
		{
			_logger.LogFailed(
				LogLevel.Error,
				DomainLogEvents.User.ChangePhone,
				LogLayers.ApplicationService,
				ApplicationLogOperations.Users.ChangePhone,
				"User phone change failed.",
				ex,
				properties);
			throw;
		}
	}

	public Task<IReadOnlyList<UserPublicModel>> GetUsersByRegionAsync(string regionName, CancellationToken cancellationToken)
		=> ExecuteReadAsync(
			DomainLogEvents.User.GetUsersByRegion,
			ApplicationLogOperations.Users.GetUsersByRegion,
			() => _users.GetByRegionAsync(regionName, cancellationToken),
			cancellationToken,
			result => new (string Name, object? Value)[]
			{
				("Count", result.Count),
			},
			("RegionName", regionName));

	public Task<IReadOnlyList<UserPublicModel>> GetUsersBySettlementAsync(string settlementName, CancellationToken cancellationToken)
		=> ExecuteReadAsync(
			DomainLogEvents.User.GetUsersBySettlement,
			ApplicationLogOperations.Users.GetUsersBySettlement,
			() => _users.GetBySettlementAsync(settlementName, cancellationToken),
			cancellationToken,
			result => new (string Name, object? Value)[]
			{
				("Count", result.Count),
			},
			("SettlementName", settlementName));

	public Task<IReadOnlyList<UserPublicModel>> GetUsersByRegionAndSettlementAsync(string regionName, string settlementName, CancellationToken cancellationToken)
		=> ExecuteReadAsync(
			DomainLogEvents.User.GetUsersByRegionAndSettlement,
			ApplicationLogOperations.Users.GetUsersByRegionAndSettlement,
			() => _users.GetByRegionAndSettlementAsync(regionName, settlementName, cancellationToken),
			cancellationToken,
			result => new (string Name, object? Value)[]
			{
				("Count", result.Count),
			},
			("RegionName", regionName),
			("SettlementName", settlementName));

	public Task<IReadOnlyList<UserPublicModel>> GetUsersAsync(
		string? role,
		string? regionName,
		string? settlementName,
		CancellationToken cancellationToken)
		=> ExecuteReadAsync(
			DomainLogEvents.User.GetUsers,
			ApplicationLogOperations.Users.GetUsers,
			async () =>
			{
				if(string.Equals(role, UserRoles.Coordinator, StringComparison.Ordinal))
				{
					var users = await _users.GetByFiltersAsync(
						role: null,
						regionName,
						settlementName,
						cancellationToken);

					return users
						.Where(x =>
							string.Equals(x.Role, UserRoles.Coordinator, StringComparison.OrdinalIgnoreCase)
							|| string.Equals(x.Role, UserRoles.Admin, StringComparison.OrdinalIgnoreCase))
						.ToList();
				}

				return await _users.GetByFiltersAsync(
					role,
					regionName,
					settlementName,
					cancellationToken);
			},
			cancellationToken,
			result => new (string Name, object? Value)[]
			{
				("Count", result.Count),
			},
			("Role", role),
			("RegionName", regionName),
			("SettlementName", settlementName));

	public Task<string?> GetRoleAsync(Guid userId, CancellationToken cancellationToken)
		=> ExecuteReadAsync(
			DomainLogEvents.User.GetRole,
			ApplicationLogOperations.Users.GetRole,
			() => _users.GetRoleAsync(userId, cancellationToken),
			cancellationToken,
			result => new (string Name, object? Value)[]
			{
				("Found", result is not null),
				("Role", result),
			},
			("UserId", userId));

	public async Task<UserRoleChangeResult> SetCoordinatorRoleAsync(
		Guid actorUserId,
		string actorUserPassword,
		Guid targetUserId,
		bool isCoordinator,
		CancellationToken cancellationToken)
	{
		var properties = new (string Name, object? Value)[]
		{
			("ActorUserId", actorUserId),
			("TargetUserId", targetUserId),
			("IsCoordinator", isCoordinator),
		};

		using var scope = _logger.BeginExecutionScope(
			DomainLogEvents.User.ChangeCoordinatorRole,
			LogLayers.ApplicationService,
			ApplicationLogOperations.Users.ChangeCoordinatorRole,
			properties);
		_logger.LogStarted(
			DomainLogEvents.User.ChangeCoordinatorRole,
			LogLayers.ApplicationService,
			ApplicationLogOperations.Users.ChangeCoordinatorRole,
			"User role change started.",
			properties);

		try
		{
			if(actorUserId == Guid.Empty || targetUserId == Guid.Empty || string.IsNullOrWhiteSpace(actorUserPassword))
			{
				_logger.LogRejected(
					LogLevel.Warning,
					DomainLogEvents.User.ChangeCoordinatorRole,
					LogLayers.ApplicationService,
					ApplicationLogOperations.Users.ChangeCoordinatorRole,
					"Coordinator role change rejected by validation.",
					StructuredLog.Combine(properties, ("Error", UserRoleChangeError.ValidationFailed)));
				return UserRoleChangeResult.Fail(UserRoleChangeError.ValidationFailed);
			}

			var actorAuth = await _actorAccess.AuthenticateAsync(actorUserId, actorUserPassword, cancellationToken);
			if(!actorAuth.IsSuccess)
			{
				_logger.LogRejected(
					LogLevel.Warning,
					DomainLogEvents.User.ChangeCoordinatorRole,
					LogLayers.ApplicationService,
					ApplicationLogOperations.Users.ChangeCoordinatorRole,
					"Coordinator role change rejected. Invalid actor credentials.",
					StructuredLog.Combine(properties, ("Error", UserRoleChangeError.InvalidCredentials)));
				return UserRoleChangeResult.Fail(UserRoleChangeError.InvalidCredentials);
			}

			if(!UserRoleRules.IsAdmin(actorAuth.Actor!.Role))
			{
				_logger.LogRejected(
					LogLevel.Warning,
					DomainLogEvents.User.ChangeCoordinatorRole,
					LogLayers.ApplicationService,
					ApplicationLogOperations.Users.ChangeCoordinatorRole,
					"Coordinator role change rejected. Actor is not admin.",
					StructuredLog.Combine(
						properties,
						("ActorRole", actorAuth.Actor.Role),
						("Error", UserRoleChangeError.Forbidden)));
				return UserRoleChangeResult.Fail(UserRoleChangeError.Forbidden);
			}

			var target = await _users.GetInternalByIdAsync(targetUserId, cancellationToken);
			if(target is null)
			{
				_logger.LogRejected(
					LogLevel.Warning,
					DomainLogEvents.User.ChangeCoordinatorRole,
					LogLayers.ApplicationService,
					ApplicationLogOperations.Users.ChangeCoordinatorRole,
					"Coordinator role change rejected. Target user not found.",
					StructuredLog.Combine(properties, ("Error", UserRoleChangeError.UserNotFound)));
				return UserRoleChangeResult.Fail(UserRoleChangeError.UserNotFound);
			}

			if(UserRoleRules.IsAdmin(target.Role) || UserRoleRules.IsBanned(target.Role))
			{
				_logger.LogRejected(
					LogLevel.Warning,
					DomainLogEvents.User.ChangeCoordinatorRole,
					LogLayers.ApplicationService,
					ApplicationLogOperations.Users.ChangeCoordinatorRole,
					"User role change rejected. Target role change is not allowed.",
					StructuredLog.Combine(
						properties,
						("TargetRole", target.Role),
						("Error", UserRoleChangeError.RoleChangeNotAllowed)));
				return UserRoleChangeResult.Fail(UserRoleChangeError.RoleChangeNotAllowed);
			}

			var nextRole = isCoordinator ? UserRoles.Coordinator : UserRoles.Activist;
			var changed = await _users.SetRoleAsync(targetUserId, nextRole, cancellationToken);
			if(!changed)
			{
				_logger.LogRejected(
					LogLevel.Warning,
					DomainLogEvents.User.ChangeCoordinatorRole,
					LogLayers.ApplicationService,
					ApplicationLogOperations.Users.ChangeCoordinatorRole,
					"Coordinator role change rejected. Target user disappeared before save.",
					StructuredLog.Combine(properties, ("Error", UserRoleChangeError.UserNotFound)));
				return UserRoleChangeResult.Fail(UserRoleChangeError.UserNotFound);
			}
			_logger.LogCompleted(
				LogLevel.Information,
				DomainLogEvents.User.ChangeCoordinatorRole,
				LogLayers.ApplicationService,
				ApplicationLogOperations.Users.ChangeCoordinatorRole,
				"User role change completed.",
				StructuredLog.Combine(properties, ("NewRole", nextRole)));
			return UserRoleChangeResult.Success();
		}
		catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
		{
			_logger.LogAborted(
				LogLevel.Information,
				DomainLogEvents.User.ChangeCoordinatorRole,
				LogLayers.ApplicationService,
				ApplicationLogOperations.Users.ChangeCoordinatorRole,
				"User role change aborted.",
				properties);
			throw;
		}
		catch(Exception ex)
		{
			_logger.LogFailed(
				LogLevel.Error,
				DomainLogEvents.User.ChangeCoordinatorRole,
				LogLayers.ApplicationService,
				ApplicationLogOperations.Users.ChangeCoordinatorRole,
				"User role change failed.",
				ex,
				properties);
			throw;
		}
	}

	public async Task<UserRoleChangeResult> SetBannedRoleAsync(
		Guid actorUserId,
		string actorUserPassword,
		Guid targetUserId,
		bool isBanned,
		CancellationToken cancellationToken)
	{
		var properties = new (string Name, object? Value)[]
		{
			("ActorUserId", actorUserId),
			("TargetUserId", targetUserId),
			("IsBanned", isBanned),
		};

		using var scope = _logger.BeginExecutionScope(
			DomainLogEvents.User.ChangeCoordinatorRole,
			LogLayers.ApplicationService,
			ApplicationLogOperations.Users.ChangeCoordinatorRole,
			properties);
		_logger.LogStarted(
			DomainLogEvents.User.ChangeCoordinatorRole,
			LogLayers.ApplicationService,
			ApplicationLogOperations.Users.ChangeCoordinatorRole,
			"User role change started.",
			properties);

		try
		{
			if(actorUserId == Guid.Empty || targetUserId == Guid.Empty || string.IsNullOrWhiteSpace(actorUserPassword))
			{
				_logger.LogRejected(
					LogLevel.Warning,
					DomainLogEvents.User.ChangeCoordinatorRole,
					LogLayers.ApplicationService,
					ApplicationLogOperations.Users.ChangeCoordinatorRole,
					"User role change rejected by validation.",
					StructuredLog.Combine(properties, ("Error", UserRoleChangeError.ValidationFailed)));
				return UserRoleChangeResult.Fail(UserRoleChangeError.ValidationFailed);
			}

			var actorAuth = await _actorAccess.AuthenticateAsync(actorUserId, actorUserPassword, cancellationToken);
			if(!actorAuth.IsSuccess)
			{
				_logger.LogRejected(
					LogLevel.Warning,
					DomainLogEvents.User.ChangeCoordinatorRole,
					LogLayers.ApplicationService,
					ApplicationLogOperations.Users.ChangeCoordinatorRole,
					"User role change rejected. Invalid actor credentials.",
					StructuredLog.Combine(properties, ("Error", UserRoleChangeError.InvalidCredentials)));
				return UserRoleChangeResult.Fail(UserRoleChangeError.InvalidCredentials);
			}

			if(!UserRoleRules.IsAdmin(actorAuth.Actor!.Role))
			{
				_logger.LogRejected(
					LogLevel.Warning,
					DomainLogEvents.User.ChangeCoordinatorRole,
					LogLayers.ApplicationService,
					ApplicationLogOperations.Users.ChangeCoordinatorRole,
					"User role change rejected. Actor is not admin.",
					StructuredLog.Combine(
						properties,
						("ActorRole", actorAuth.Actor.Role),
						("Error", UserRoleChangeError.Forbidden)));
				return UserRoleChangeResult.Fail(UserRoleChangeError.Forbidden);
			}

			var target = await _users.GetInternalByIdAsync(targetUserId, cancellationToken);
			if(target is null)
			{
				_logger.LogRejected(
					LogLevel.Warning,
					DomainLogEvents.User.ChangeCoordinatorRole,
					LogLayers.ApplicationService,
					ApplicationLogOperations.Users.ChangeCoordinatorRole,
					"User role change rejected. Target user not found.",
					StructuredLog.Combine(properties, ("Error", UserRoleChangeError.UserNotFound)));
				return UserRoleChangeResult.Fail(UserRoleChangeError.UserNotFound);
			}

			if(isBanned)
			{
				if(UserRoleRules.IsAdmin(target.Role) || UserRoleRules.IsBanned(target.Role))
				{
					_logger.LogRejected(
						LogLevel.Warning,
						DomainLogEvents.User.ChangeCoordinatorRole,
						LogLayers.ApplicationService,
						ApplicationLogOperations.Users.ChangeCoordinatorRole,
						"User role change rejected. Target role change is not allowed.",
						StructuredLog.Combine(
							properties,
							("TargetRole", target.Role),
							("Error", UserRoleChangeError.RoleChangeNotAllowed)));
					return UserRoleChangeResult.Fail(UserRoleChangeError.RoleChangeNotAllowed);
				}
			}
			else
			{
				if(!UserRoleRules.IsBanned(target.Role))
				{
					_logger.LogRejected(
						LogLevel.Warning,
						DomainLogEvents.User.ChangeCoordinatorRole,
						LogLayers.ApplicationService,
						ApplicationLogOperations.Users.ChangeCoordinatorRole,
						"User role change rejected. Target role change is not allowed.",
						StructuredLog.Combine(
							properties,
							("TargetRole", target.Role),
							("Error", UserRoleChangeError.RoleChangeNotAllowed)));
					return UserRoleChangeResult.Fail(UserRoleChangeError.RoleChangeNotAllowed);
				}
			}

			var nextRole = isBanned ? UserRoles.Banned : UserRoles.Activist;
			var changed = await _users.SetRoleAsync(targetUserId, nextRole, cancellationToken);
			if(!changed)
			{
				_logger.LogRejected(
					LogLevel.Warning,
					DomainLogEvents.User.ChangeCoordinatorRole,
					LogLayers.ApplicationService,
					ApplicationLogOperations.Users.ChangeCoordinatorRole,
					"User role change rejected. Target user disappeared before save.",
					StructuredLog.Combine(properties, ("Error", UserRoleChangeError.UserNotFound)));
				return UserRoleChangeResult.Fail(UserRoleChangeError.UserNotFound);
			}

			_logger.LogCompleted(
				LogLevel.Information,
				DomainLogEvents.User.ChangeCoordinatorRole,
				LogLayers.ApplicationService,
				ApplicationLogOperations.Users.ChangeCoordinatorRole,
				"User role change completed.",
				StructuredLog.Combine(properties, ("NewRole", nextRole)));
			return UserRoleChangeResult.Success();
		}
		catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
		{
			_logger.LogAborted(
				LogLevel.Information,
				DomainLogEvents.User.ChangeCoordinatorRole,
				LogLayers.ApplicationService,
				ApplicationLogOperations.Users.ChangeCoordinatorRole,
				"User role change aborted.",
				properties);
			throw;
		}
		catch(Exception ex)
		{
			_logger.LogFailed(
				LogLevel.Error,
				DomainLogEvents.User.ChangeCoordinatorRole,
				LogLayers.ApplicationService,
				ApplicationLogOperations.Users.ChangeCoordinatorRole,
				"User role change failed.",
				ex,
				properties);
			throw;
		}
	}

	private async Task<T> ExecuteReadAsync<T>(
		string eventName,
		string operationName,
		Func<Task<T>> action,
		CancellationToken cancellationToken,
		Func<T, (string Name, object? Value)[]>? resultPropertiesFactory = null,
		params (string Name, object? Value)[] properties)
	{
		using var scope = _logger.BeginExecutionScope(eventName, LogLayers.ApplicationService, operationName, properties);

		_logger.LogStarted(
			eventName,
			LogLayers.ApplicationService,
			operationName,
			"User application read operation started.",
			properties);

		try
		{
			var result = await action();
			var resultProperties = resultPropertiesFactory?.Invoke(result) ?? Array.Empty<(string Name, object? Value)>();

			_logger.LogCompleted(
				LogLevel.Debug,
				eventName,
				LogLayers.ApplicationService,
				operationName,
				"User application read operation completed.",
				StructuredLog.Combine(properties, resultProperties));

			return result;
		}
		catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
		{
			_logger.LogAborted(
				LogLevel.Information,
				eventName,
				LogLayers.ApplicationService,
				operationName,
				"User application read operation aborted.",
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
				"User application read operation failed.",
				ex,
				properties);
			throw;
		}
	}

	private static UserPublicModel ToPublic(UserInternalModel u) =>
		new(u.Id, u.LastName, u.FirstName, u.MiddleName, u.Gender, u.PhoneNumber, u.BirthDate, u.RegionName, u.SettlementName, u.Role, u.IsPhoneConfirmed, u.AvatarImageUrl);

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