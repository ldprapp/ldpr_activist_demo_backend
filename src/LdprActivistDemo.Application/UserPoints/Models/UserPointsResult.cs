namespace LdprActivistDemo.Application.UserPoints.Models;

public sealed record UserPointsResult<T>(bool IsSuccess, UserPointsError Error, T? Value)
{
	public static UserPointsResult<T> Ok(T value) => new(true, UserPointsError.None, value);

	public static UserPointsResult<T> Fail(UserPointsError error) => new(false, error, default);
}