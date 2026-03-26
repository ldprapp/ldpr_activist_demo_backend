namespace LdprActivistDemo.Application.Referrals.Models;

public enum ReferralTemplateUpdateError
{
	None = 0,
	ValidationFailed = 1,
	InvalidCredentials = 2,
	Forbidden = 3,
}

public readonly record struct ReferralTemplateUpdateResult(string InviteTextTemplate, ReferralTemplateUpdateError Error)
{
	public bool IsSuccess => Error == ReferralTemplateUpdateError.None;

	public static ReferralTemplateUpdateResult Success(string inviteTextTemplate)
		=> new(inviteTextTemplate, ReferralTemplateUpdateError.None);

	public static ReferralTemplateUpdateResult Fail(ReferralTemplateUpdateError error)
		=> new(string.Empty, error);
}