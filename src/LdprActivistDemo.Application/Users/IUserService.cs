using LdprActivistDemo.Application.Users.Models;

namespace LdprActivistDemo.Application.Users;

public interface IUserService
{
	Task<Guid> RegisterAsync(UserCreateModel model, CancellationToken cancellationToken);
	Task<bool> ConfirmPhoneAsync(string phoneNumber, string otpCode, CancellationToken cancellationToken);
	Task<LoginResult> LoginAsync(string phoneNumber, string password, CancellationToken cancellationToken);
	Task<UserPublicModel?> GetByPhoneAsync(string phoneNumber, CancellationToken cancellationToken);
	Task<UserPublicModel?> GetByIdAsync(Guid userId, CancellationToken cancellationToken);
	Task<bool> ChangePasswordAsync(Guid userId, string oldPassword, string newPassword, CancellationToken cancellationToken);
	Task<bool> UpdateAsync(UserUpdateModel model, string actorPassword, CancellationToken cancellationToken);
	Task<bool> ChangePhoneAsync(Guid userId, string password, string newPhoneNumber, string otpCode, CancellationToken cancellationToken);
	Task<IReadOnlyList<UserFullNameModel>> GetUsersByRegionAsync(int regionId, CancellationToken cancellationToken);
	Task<IReadOnlyList<UserFullNameModel>> GetUsersByCityAsync(int cityId, CancellationToken cancellationToken);
	Task<IReadOnlyList<UserFullNameModel>> GetUsersByRegionAndCityAsync(int regionId, int cityId, CancellationToken cancellationToken);
	Task<bool> IsAdminAsync(Guid userId, CancellationToken cancellationToken);
	Task<IReadOnlyList<UserPublicModel>> GetAdminsAsync(CancellationToken cancellationToken);
}