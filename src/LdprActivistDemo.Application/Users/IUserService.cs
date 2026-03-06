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
	Task<IReadOnlyList<UserPublicModel>> GetUsersByRegionAsync(string regionName, CancellationToken cancellationToken);
	Task<IReadOnlyList<UserPublicModel>> GetUsersByCityAsync(string cityName, CancellationToken cancellationToken);
	Task<IReadOnlyList<UserPublicModel>> GetUsersByRegionAndCityAsync(string regionName, string cityName, CancellationToken cancellationToken);
	Task<bool> IsAdminAsync(Guid userId, CancellationToken cancellationToken);
	Task<IReadOnlyList<UserPublicModel>> GetAdminsAsync(CancellationToken cancellationToken);
	Task<IReadOnlyList<UserPublicModel>> GetAdminsAsync(int? start, int? end, CancellationToken cancellationToken);
}