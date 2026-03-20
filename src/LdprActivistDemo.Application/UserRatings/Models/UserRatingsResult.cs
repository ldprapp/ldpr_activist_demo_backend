namespace LdprActivistDemo.Application.UserRatings.Models;

public sealed record UserRatingsResult<T>(bool IsSuccess, UserRatingsError Error, T? Value)
{
	public static UserRatingsResult<T> Ok(T value) => new(true, UserRatingsError.None, value);

	public static UserRatingsResult<T> Fail(UserRatingsError error) => new(false, error, default);
}