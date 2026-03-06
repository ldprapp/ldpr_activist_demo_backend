using LdprActivistDemo.Application.Otp;
using LdprActivistDemo.Application.Users.Models;

namespace LdprActivistDemo.Application.Users;

public sealed class UserService : IUserService
{
	private readonly IUserRepository _users;
	private readonly IOtpService _otp;
	private readonly IUnconfirmedUserCleanupScheduler _cleanupScheduler;
	private readonly IActorAccessService _actorAccess;

	public UserService(IUserRepository users, IOtpService otp, IUnconfirmedUserCleanupScheduler cleanupScheduler, IActorAccessService actorAccess)
	{
		_users = users;
		_otp = otp;
		_cleanupScheduler = cleanupScheduler;
		_actorAccess = actorAccess;
	}

	public async Task<Guid> RegisterAsync(UserCreateModel model, CancellationToken cancellationToken)
	{
		var confirmedExists = await _users.ExistsConfirmedByPhoneAsync(model.PhoneNumber, cancellationToken);
		if(confirmedExists)
		{
			throw new InvalidOperationException("PhoneNumber already exists.");
		}

		await _users.DeleteUnconfirmedByPhoneAsync(model.PhoneNumber, cancellationToken);

		var userId = await _users.CreateAsync(model, cancellationToken);

		_cleanupScheduler.Schedule(userId);

		return userId;
	}

	public async Task<bool> ConfirmPhoneAsync(string phoneNumber, string otpCode, CancellationToken cancellationToken)
	{
		var ok = await _otp.VerifyAsync(phoneNumber, otpCode, cancellationToken);
		if(!ok)
		{
			return false;
		}

		return await _users.SetPhoneConfirmedAsync(phoneNumber, true, cancellationToken);
	}

	public async Task<LoginResult> LoginAsync(string phoneNumber, string password, CancellationToken cancellationToken)
	{
		var passwordOk = await _users.ValidatePasswordAsync(phoneNumber, password, cancellationToken);

		if(!passwordOk)
		{
			return LoginResult.Fail(LoginError.InvalidCredentials);
		}

		var u = await _users.GetInternalByPhoneAsync(phoneNumber, cancellationToken);
		if(u is null)
		{
			return LoginResult.Fail(LoginError.InvalidCredentials);
		}

		if(!u.IsPhoneConfirmed)
		{
			return LoginResult.Fail(LoginError.PhoneNotConfirmed);
		}

		return LoginResult.Ok(ToPublic(u));
	}

	public Task<UserPublicModel?> GetByPhoneAsync(string phoneNumber, CancellationToken cancellationToken) =>
		_users.GetPublicByPhoneAsync(phoneNumber, cancellationToken);

	public Task<UserPublicModel?> GetByIdAsync(Guid userId, CancellationToken cancellationToken) =>
		_users.GetPublicByIdAsync(userId, cancellationToken);

	public Task<bool> ChangePasswordAsync(Guid userId, string newPassword, CancellationToken cancellationToken) =>
		_users.SetPasswordAsync(userId, newPassword, cancellationToken);

	public Task<bool> UpdateAsync(UserUpdateModel model, CancellationToken cancellationToken) =>
		_users.UpdateAsync(model, cancellationToken);

	public async Task<bool> ChangePhoneAsync(Guid userId, string newPhoneNumber, string otpCode, CancellationToken cancellationToken)
	{
		var otpOk = await _otp.VerifyAsync(newPhoneNumber, otpCode, cancellationToken);
		if(!otpOk)
		{
			return false;
		}

		var changed = await _users.ChangePhoneAsync(
			userId,
			newPhoneNumber,
			cancellationToken);

		if(!changed)
		{
			return false;
		}

		await _users.SetPhoneConfirmedAsync(
			newPhoneNumber,
			true,
			cancellationToken);

		return true;
	}

	public Task<IReadOnlyList<UserPublicModel>> GetUsersByRegionAsync(string regionName, CancellationToken cancellationToken) =>
		_users.GetByRegionAsync(regionName, cancellationToken);

	public Task<IReadOnlyList<UserPublicModel>> GetUsersByCityAsync(string cityName, CancellationToken cancellationToken) =>
		_users.GetByCityAsync(cityName, cancellationToken);

	public Task<IReadOnlyList<UserPublicModel>> GetUsersByRegionAndCityAsync(string regionName, string cityName, CancellationToken cancellationToken) =>
		_users.GetByRegionAndCityAsync(regionName, cityName, cancellationToken);

	public Task<IReadOnlyList<UserPublicModel>> GetUsersAsync(string? role, string? regionName, string? cityName, CancellationToken cancellationToken) =>
		_users.GetByFiltersAsync(role, regionName, cityName, cancellationToken);

	public Task<string?> GetRoleAsync(Guid userId, CancellationToken cancellationToken) =>
		_users.GetRoleAsync(userId, cancellationToken);

	public async Task<UserRoleChangeResult> SetCoordinatorRoleAsync(
		Guid actorUserId,
		string actorUserPassword,
		Guid targetUserId,
		bool isCoordinator,
		CancellationToken cancellationToken)
	{
		if(actorUserId == Guid.Empty || targetUserId == Guid.Empty || string.IsNullOrWhiteSpace(actorUserPassword))
		{
			return UserRoleChangeResult.Fail(UserRoleChangeError.ValidationFailed);
		}

		var actorAuth = await _actorAccess.AuthenticateAsync(actorUserId, actorUserPassword, cancellationToken);
		if(!actorAuth.IsSuccess)
		{
			return UserRoleChangeResult.Fail(UserRoleChangeError.InvalidCredentials);
		}

		if(!UserRoleRules.IsAdmin(actorAuth.Actor!.Role))
		{
			return UserRoleChangeResult.Fail(UserRoleChangeError.Forbidden);
		}

		var target = await _users.GetInternalByIdAsync(targetUserId, cancellationToken);
		if(target is null)
		{
			return UserRoleChangeResult.Fail(UserRoleChangeError.UserNotFound);
		}

		if(UserRoleRules.IsAdmin(target.Role))
		{
			return UserRoleChangeResult.Fail(UserRoleChangeError.RoleChangeNotAllowed);
		}

		var nextRole = isCoordinator ? UserRoles.Coordinator : UserRoles.Activist;
		var changed = await _users.SetRoleAsync(targetUserId, nextRole, cancellationToken);
		return changed ? UserRoleChangeResult.Success() : UserRoleChangeResult.Fail(UserRoleChangeError.UserNotFound);
	}

	private static UserPublicModel ToPublic(UserInternalModel u) =>
		new(u.Id, u.LastName, u.FirstName, u.MiddleName, u.Gender, u.PhoneNumber, u.BirthDate, u.RegionName, u.CityName, u.Role, u.IsPhoneConfirmed, u.AvatarImageUrl);
}