namespace LdprActivistDemo.Application.Referrals.Models;

public enum ReferralSettingsUpdateError
{
	None = 0,
	ValidationFailed = 1,
	InvalidCredentials = 2,
	Forbidden = 3,
}

public readonly record struct ReferralSettingsUpdateResult(
	string InviteTextTemplate,
	int InviterRewardPoints,
	int InvitedUserRewardPoints,
	ReferralSettingsUpdateError Error)
{
	public bool IsSuccess => Error == ReferralSettingsUpdateError.None;

	public static ReferralSettingsUpdateResult Success(
		 string inviteTextTemplate,
		 int inviterRewardPoints,
		 int invitedUserRewardPoints)
		=> new(
 inviteTextTemplate,
 inviterRewardPoints,
 invitedUserRewardPoints,
 ReferralSettingsUpdateError.None);

	public static ReferralSettingsUpdateResult Fail(ReferralSettingsUpdateError error)
		=> new(string.Empty, 0, 0, error);
}