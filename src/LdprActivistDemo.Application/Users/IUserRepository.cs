using LdprActivistDemo.Application.Users.Models;

namespace LdprActivistDemo.Application.Users;

public interface IUserRepository
{
	Task<UserInternalModel?> GetInternalByIdAsync(Guid userId, CancellationToken cancellationToken);
	Task<UserInternalModel?> GetInternalByPhoneAsync(string phoneNumber, CancellationToken cancellationToken);

	Task<UserPublicModel?> GetPublicByIdAsync(Guid userId, CancellationToken cancellationToken);
	Task<UserPublicModel?> GetPublicByPhoneAsync(string phoneNumber, CancellationToken cancellationToken);

	Task<Guid> CreateAsync(UserCreateModel model, CancellationToken cancellationToken);

	Task<bool> ValidatePasswordAsync(string phoneNumber, string passwordHash, CancellationToken cancellationToken);
	Task<bool> ValidatePasswordAsync(Guid userId, string passwordHash, CancellationToken cancellationToken);

	Task<bool> SetPhoneConfirmedAsync(string phoneNumber, bool isConfirmed, CancellationToken cancellationToken);
	Task<bool> ChangePasswordAsync(Guid userId, string oldPasswordHash, string newPasswordHash, CancellationToken cancellationToken);
	Task<bool> UpdateAsync(UserUpdateModel model, CancellationToken cancellationToken);
	Task<bool> ChangePhoneAsync(Guid userId, string passwordHash, string newPhoneNumber, CancellationToken cancellationToken);

	Task<IReadOnlyList<UserFullNameModel>> GetByRegionAsync(int regionId, CancellationToken cancellationToken);
	Task<IReadOnlyList<UserFullNameModel>> GetByCityAsync(int cityId, CancellationToken cancellationToken);

	Task<bool> IsAdminAsync(Guid userId, CancellationToken cancellationToken);
	Task<IReadOnlyList<Guid>> GetAllAdminIdsAsync(CancellationToken cancellationToken);

	Task<bool> AddPointsAsync(Guid userId, int pointsToAdd, CancellationToken cancellationToken);
}