using LdprActivistDemo.Application.Otp;
using LdprActivistDemo.Application.Users.Models;

namespace LdprActivistDemo.Application.Users;

public sealed class UserService : IUserService
{
	private readonly IUserRepository _users;
	private readonly IOtpService _otp;
	private readonly IUnconfirmedUserCleanupScheduler _cleanupScheduler;

	public UserService(IUserRepository users, IOtpService otp, IUnconfirmedUserCleanupScheduler cleanupScheduler)
	{
		_users = users;
		_otp = otp;
		_cleanupScheduler = cleanupScheduler;
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

	public Task<bool> ChangePasswordAsync(Guid userId, string oldPassword, string newPassword, CancellationToken cancellationToken) =>
		_users.ChangePasswordAsync(userId, oldPassword, newPassword, cancellationToken);

	public Task<bool> UpdateAsync(UserUpdateModel model, string actorPassword, CancellationToken cancellationToken) =>
		_users.UpdateAsync(model, actorPassword, cancellationToken);

	public async Task<bool> ChangePhoneAsync(Guid userId, string password, string newPhoneNumber, string otpCode, CancellationToken cancellationToken)
	{
		var otpOk = await _otp.VerifyAsync(newPhoneNumber, otpCode, cancellationToken);
		if(!otpOk)
		{
			return false;
		}

		var changed = await _users.ChangePhoneAsync(
			userId,
			password,
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

	public Task<IReadOnlyList<UserPublicModel>> GetUsersByRegionAsync(int regionId, CancellationToken cancellationToken) =>
		_users.GetByRegionAsync(regionId, cancellationToken);

	public Task<IReadOnlyList<UserPublicModel>> GetUsersByCityAsync(int cityId, CancellationToken cancellationToken) =>
		_users.GetByCityAsync(cityId, cancellationToken);

	public Task<IReadOnlyList<UserPublicModel>> GetUsersByRegionAndCityAsync(int regionId, int cityId, CancellationToken cancellationToken) =>
		_users.GetByRegionAndCityAsync(regionId, cityId, cancellationToken);

	public Task<bool> IsAdminAsync(Guid userId, CancellationToken cancellationToken) =>
		_users.IsAdminAsync(userId, cancellationToken);

	public Task<IReadOnlyList<UserPublicModel>> GetAdminsAsync(CancellationToken cancellationToken) =>
		GetAdminsAsync(start: null, end: null, cancellationToken);

	public Task<IReadOnlyList<UserPublicModel>> GetAdminsAsync(int? start, int? end, CancellationToken cancellationToken) =>
		_users.GetAdminsAsync(start, end, cancellationToken);

	private static UserPublicModel ToPublic(UserInternalModel u) =>
		new(u.Id, u.LastName, u.FirstName, u.MiddleName, u.Gender, u.PhoneNumber, u.BirthDate, u.RegionId, u.CityId, u.IsPhoneConfirmed, u.Points, u.AvatarImageUrl);
}