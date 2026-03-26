namespace LdprActivistDemo.Application.Referrals.Models;

public enum ReferralSettingsReadError
{
	None = 0,
	ValidationFailed = 1,
	InvalidCredentials = 2,
	Forbidden = 3,
}

public readonly record struct ReferralSettingsReadResult(
	string InviteTextTemplate,
	int InviterRewardPoints,
	int InvitedUserRewardPoints,
	ReferralSettingsReadError Error)
{
	public bool IsSuccess => Error == ReferralSettingsReadError.None;

	public static ReferralSettingsReadResult Success(
		string inviteTextTemplate,
		int inviterRewardPoints,
		int invitedUserRewardPoints)
		=> new(
			inviteTextTemplate,
			inviterRewardPoints,
			invitedUserRewardPoints,
			ReferralSettingsReadError.None);

	public static ReferralSettingsReadResult Fail(ReferralSettingsReadError error)
		=> new(string.Empty, 0, 0, error);
}