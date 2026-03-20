namespace LdprActivistDemo.Application.UserRatings.Models;

public enum UserRatingsAdminError
{
	None = 0,
	ValidationFailed = 1,
	InvalidCredentials = 2,
	Forbidden = 3,
}

/// <summary>
/// Результат административной операции управления расписанием или ручным запуском рейтингов.
/// </summary>
public sealed record UserRatingsAdminResult<T>(bool IsSuccess, UserRatingsAdminError Error, T? Value)
{
	public static UserRatingsAdminResult<T> Ok(T value) => new(true, UserRatingsAdminError.None, value);

	public static UserRatingsAdminResult<T> Fail(UserRatingsAdminError error) => new(false, error, default);
}