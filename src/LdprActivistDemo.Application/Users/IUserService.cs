using LdprActivistDemo.Application.Users.Models;

namespace LdprActivistDemo.Application.Users;

public interface IUserService
{
	Task<Guid> RegisterAsync(UserCreateModel model, CancellationToken cancellationToken);
	Task<bool> ConfirmPhoneAsync(string phoneNumber, string otpCode, CancellationToken cancellationToken);
	Task<bool> LoginAsync(string phoneNumber, string passwordHash, CancellationToken cancellationToken);

	Task<UserPublicModel?> GetByPhoneAsync(string phoneNumber, CancellationToken cancellationToken);
	Task<UserPublicModel?> GetByIdAsync(Guid userId, CancellationToken cancellationToken);

	Task<bool> ChangePasswordAsync(Guid userId, string oldPasswordHash, string newPasswordHash, CancellationToken cancellationToken);
	Task<bool> UpdateAsync(UserUpdateModel model, CancellationToken cancellationToken);
	Task<bool> ChangePhoneAsync(Guid userId, string passwordHash, string newPhoneNumber, CancellationToken cancellationToken);

	Task<IReadOnlyList<UserFullNameModel>> GetUsersByRegionAsync(int regionId, CancellationToken cancellationToken);
	Task<IReadOnlyList<UserFullNameModel>> GetUsersByCityAsync(int cityId, CancellationToken cancellationToken);

	Task<bool> IsAdminAsync(Guid userId, CancellationToken cancellationToken);
	Task<IReadOnlyList<Guid>> GetAdminIdsAsync(CancellationToken cancellationToken);
}