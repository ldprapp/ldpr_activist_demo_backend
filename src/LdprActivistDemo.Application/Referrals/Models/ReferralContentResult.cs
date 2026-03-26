namespace LdprActivistDemo.Application.Referrals.Models;

public enum ReferralContentError
{
	None = 0,
	ValidationFailed = 1,
	InvalidCredentials = 2,
	Forbidden = 3,
	UserNotFound = 4,
}

public readonly record struct ReferralContentResult(string Content, ReferralContentError Error)
{
	public bool IsSuccess => Error == ReferralContentError.None;

	public static ReferralContentResult Success(string content)
		=> new(content, ReferralContentError.None);

	public static ReferralContentResult Fail(ReferralContentError error)
		=> new(string.Empty, error);
}