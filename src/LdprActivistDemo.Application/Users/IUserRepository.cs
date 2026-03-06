using LdprActivistDemo.Application.Users.Models;

namespace LdprActivistDemo.Application.Users;

public interface IUserRepository
{
	Task<UserInternalModel?> GetInternalByIdAsync(Guid userId, CancellationToken cancellationToken);
	Task<UserInternalModel?> GetInternalByPhoneAsync(string phoneNumber, CancellationToken cancellationToken);

	Task<UserPublicModel?> GetPublicByIdAsync(Guid userId, CancellationToken cancellationToken);
	Task<UserPublicModel?> GetPublicByPhoneAsync(string phoneNumber, CancellationToken cancellationToken);

	Task<bool> ExistsConfirmedByPhoneAsync(string phoneNumber, CancellationToken cancellationToken);
	Task<bool> DeleteUnconfirmedByPhoneAsync(string phoneNumber, CancellationToken cancellationToken);

	Task<bool> DeleteUnconfirmedByIdAsync(Guid userId, CancellationToken cancellationToken);
	Task<int> DeleteAllUnconfirmedAsync(CancellationToken cancellationToken);

	Task<Guid> CreateAsync(UserCreateModel model, CancellationToken cancellationToken);

	Task<bool> ValidatePasswordAsync(string phoneNumber, string password, CancellationToken cancellationToken);
	Task<bool> ValidatePasswordAsync(Guid userId, string password, CancellationToken cancellationToken);

	Task<bool> SetPhoneConfirmedAsync(string phoneNumber, bool isConfirmed, CancellationToken cancellationToken);
	Task<bool> SetPasswordAsync(Guid userId, string newPassword, CancellationToken cancellationToken);
	Task<bool> UpdateAsync(UserUpdateModel model, CancellationToken cancellationToken);
	Task<bool> ChangePhoneAsync(Guid userId, string newPhoneNumber, CancellationToken cancellationToken);

	Task<IReadOnlyList<UserPublicModel>> GetByRegionAsync(string regionName, CancellationToken cancellationToken);
	Task<IReadOnlyList<UserPublicModel>> GetByCityAsync(string cityName, CancellationToken cancellationToken);
	Task<IReadOnlyList<UserPublicModel>> GetByRegionAndCityAsync(string regionName, string cityName, CancellationToken cancellationToken);

	Task<bool> IsAdminAsync(Guid userId, CancellationToken cancellationToken);
	Task<IReadOnlyList<Guid>> GetAllAdminIdsAsync(CancellationToken cancellationToken);
	Task<IReadOnlyList<UserPublicModel>> GetAdminsAsync(int? start, int? end, CancellationToken cancellationToken);

	Task<bool> AddPointsAsync(Guid userId, int pointsToAdd, CancellationToken cancellationToken);
}