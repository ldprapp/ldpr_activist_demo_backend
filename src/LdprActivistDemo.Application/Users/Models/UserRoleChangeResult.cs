namespace LdprActivistDemo.Application.Users.Models;

public enum UserRoleChangeError
{
	None = 0,
	ValidationFailed = 1,
	InvalidCredentials = 2,
	Forbidden = 3,
	UserNotFound = 4,
	RoleChangeNotAllowed = 5,
}

public readonly record struct UserRoleChangeResult(UserRoleChangeError Error)
{
	public bool IsSuccess => Error == UserRoleChangeError.None;

	public static UserRoleChangeResult Success()
		=> new(UserRoleChangeError.None);

	public static UserRoleChangeResult Fail(UserRoleChangeError error)
		=> new(error);
}