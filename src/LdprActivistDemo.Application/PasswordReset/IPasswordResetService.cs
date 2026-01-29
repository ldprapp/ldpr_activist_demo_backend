namespace LdprActivistDemo.Application.PasswordReset;

public interface IPasswordResetService
{
	Task<PasswordResetIssueResult> IssueAsync(string phoneNumber, string newPassword, CancellationToken cancellationToken);
	Task<PasswordResetConfirmResult> ConfirmAsync(string phoneNumber, string otpCode, CancellationToken cancellationToken);
}

public enum PasswordResetIssueError
{
	None = 0,
	UserNotFound = 1,
	PhoneNotConfirmed = 2,
	OtpSendFailed = 3,
	InternalError = 4,
}

public readonly record struct PasswordResetIssueResult(PasswordResetIssueError Error)
{
	public bool IsSuccess => Error == PasswordResetIssueError.None;

	public static PasswordResetIssueResult Success() => new(PasswordResetIssueError.None);
	public static PasswordResetIssueResult Fail(PasswordResetIssueError error) => new(error);
}

public enum PasswordResetConfirmError
{
	None = 0,
	UserNotFound = 1,
	PhoneNotConfirmed = 2,
	OtpInvalid = 3,
	PasswordResetExpired = 4,
	InternalError = 5,
}

public readonly record struct PasswordResetConfirmResult(PasswordResetConfirmError Error)
{
	public bool IsSuccess => Error == PasswordResetConfirmError.None;

	public static PasswordResetConfirmResult Success() => new(PasswordResetConfirmError.None);
	public static PasswordResetConfirmResult Fail(PasswordResetConfirmError error) => new(error);
}