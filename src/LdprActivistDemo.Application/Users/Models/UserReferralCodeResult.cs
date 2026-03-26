namespace LdprActivistDemo.Application.Users.Models;

public enum UserReferralCodeError
{
	None = 0,
	ValidationFailed = 1,
	InvalidCredentials = 2,
	Forbidden = 3,
	UserNotFound = 4,
}

public readonly record struct UserReferralCodeResult(int ReferralCode, UserReferralCodeError Error)
{
	public bool IsSuccess => Error == UserReferralCodeError.None;

	public static UserReferralCodeResult Success(int referralCode)
		=> new(referralCode, UserReferralCodeError.None);

	public static UserReferralCodeResult Fail(UserReferralCodeError error)
		=> new(0, error);
}