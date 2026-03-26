using LdprActivistDemo.Application.Users.Models;

namespace LdprActivistDemo.Application.Referrals.Models;

public enum ReferralInvitedUsersReadError
{
	None = 0,
	ValidationFailed = 1,
	InvalidCredentials = 2,
	Forbidden = 3,
	UserNotFound = 4,
}

public readonly record struct ReferralInvitedUsersReadResult(
	IReadOnlyList<UserPublicModel> Users,
	ReferralInvitedUsersReadError Error)
{
	public bool IsSuccess => Error == ReferralInvitedUsersReadError.None;

	public static ReferralInvitedUsersReadResult Success(
		 IReadOnlyList<UserPublicModel> users)
		=> new(
 users ?? Array.Empty<UserPublicModel>(),
 ReferralInvitedUsersReadError.None);

	public static ReferralInvitedUsersReadResult Fail(
		 ReferralInvitedUsersReadError error)
		=> new(
 Array.Empty<UserPublicModel>(),
 error);
}