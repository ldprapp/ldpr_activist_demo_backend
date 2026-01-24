using LdprActivistDemo.Application.Otp;
using LdprActivistDemo.Application.Users.Models;

namespace LdprActivistDemo.Application.Users;

public sealed class UserService : IUserService
{
	private readonly IUserRepository _users;
	private readonly IOtpService _otp;

	public UserService(IUserRepository users, IOtpService otp)
	{
		_users = users;
		_otp = otp;
	}

	public async Task<Guid> RegisterAsync(UserCreateModel model, CancellationToken cancellationToken)
	{
		var userId = await _users.CreateAsync(model, cancellationToken);
		_ = await _otp.IssueAsync(model.PhoneNumber, cancellationToken);
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

	public Task<bool> LoginAsync(string phoneNumber, string passwordHash, CancellationToken cancellationToken)
		=> _users.ValidatePasswordAsync(phoneNumber, passwordHash, cancellationToken);

	public Task<UserPublicModel?> GetByPhoneAsync(string phoneNumber, CancellationToken cancellationToken)
		=> _users.GetPublicByPhoneAsync(phoneNumber, cancellationToken);

	public Task<UserPublicModel?> GetByIdAsync(Guid userId, CancellationToken cancellationToken)
		=> _users.GetPublicByIdAsync(userId, cancellationToken);

	public Task<bool> ChangePasswordAsync(Guid userId, string oldPasswordHash, string newPasswordHash, CancellationToken cancellationToken)
		=> _users.ChangePasswordAsync(userId, oldPasswordHash, newPasswordHash, cancellationToken);

	public Task<bool> UpdateAsync(UserUpdateModel model, CancellationToken cancellationToken)
		=> _users.UpdateAsync(model, cancellationToken);

	public Task<bool> ChangePhoneAsync(Guid userId, string passwordHash, string newPhoneNumber, CancellationToken cancellationToken)
		=> _users.ChangePhoneAsync(userId, passwordHash, newPhoneNumber, cancellationToken);

	public Task<IReadOnlyList<UserFullNameModel>> GetUsersByRegionAsync(int regionId, CancellationToken cancellationToken)
		=> _users.GetByRegionAsync(regionId, cancellationToken);

	public Task<IReadOnlyList<UserFullNameModel>> GetUsersByCityAsync(int cityId, CancellationToken cancellationToken)
		=> _users.GetByCityAsync(cityId, cancellationToken);

	public Task<bool> IsAdminAsync(Guid userId, CancellationToken cancellationToken)
		=> _users.IsAdminAsync(userId, cancellationToken);

	public Task<IReadOnlyList<Guid>> GetAdminIdsAsync(CancellationToken cancellationToken)
		=> _users.GetAllAdminIdsAsync(cancellationToken);
}