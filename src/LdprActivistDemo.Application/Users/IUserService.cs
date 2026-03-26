using LdprActivistDemo.Application.Users.Models;

namespace LdprActivistDemo.Application.Users;

public interface IUserService
{
	Task<Guid> RegisterAsync(UserCreateModel model, CancellationToken cancellationToken);
	Task<bool> ConfirmPhoneAsync(string phoneNumber, string otpCode, CancellationToken cancellationToken);
	Task<LoginResult> LoginAsync(string phoneNumber, string password, CancellationToken cancellationToken);
	Task<UserPublicModel?> GetByPhoneAsync(string phoneNumber, CancellationToken cancellationToken);
	Task<UserPublicModel?> GetByIdAsync(Guid userId, CancellationToken cancellationToken);
	Task<UserReferralCodeResult> GetReferralCodeAsync(Guid actorUserId, string actorUserPassword, Guid userId, CancellationToken cancellationToken);
	Task<bool> ChangePasswordAsync(Guid userId, string newPassword, CancellationToken cancellationToken);
	Task<bool> UpdateAsync(UserUpdateModel model, CancellationToken cancellationToken);
	Task<bool> ChangePhoneAsync(Guid userId, string newPhoneNumber, string otpCode, CancellationToken cancellationToken);
	Task<bool> SetAvatarImageAsync(Guid userId, Guid? avatarImageId, CancellationToken cancellationToken);
	Task<IReadOnlyList<UserPublicModel>> GetUsersByRegionAsync(string regionName, CancellationToken cancellationToken);
	Task<IReadOnlyList<UserPublicModel>> GetUsersBySettlementAsync(string settlementName, CancellationToken cancellationToken);
	Task<IReadOnlyList<UserPublicModel>> GetUsersByRegionAndSettlementAsync(string regionName, string settlementName, CancellationToken cancellationToken);
	Task<IReadOnlyList<UserPublicModel>> GetUsersAsync(string? role, string? regionName, string? settlementName, string? search, CancellationToken cancellationToken);
	Task<string?> GetRoleAsync(Guid userId, CancellationToken cancellationToken);
	Task<UserRoleChangeResult> SetCoordinatorRoleAsync(Guid actorUserId, string actorUserPassword, Guid targetUserId, bool isCoordinator, CancellationToken cancellationToken);
	Task<UserRoleChangeResult> SetBannedRoleAsync(
		Guid actorUserId,
		string actorUserPassword,
		Guid targetUserId,
		bool isBanned,
		CancellationToken cancellationToken);
}