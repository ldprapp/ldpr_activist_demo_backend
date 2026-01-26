namespace LdprActivistDemo.Application.Users.Models;

public sealed record LoginResult(bool IsSuccess, LoginError Error, UserPublicModel? User)
{
	public static LoginResult Ok(UserPublicModel user) => new(true, LoginError.None, user);
	public static LoginResult Fail(LoginError error) => new(false, error, null);
}