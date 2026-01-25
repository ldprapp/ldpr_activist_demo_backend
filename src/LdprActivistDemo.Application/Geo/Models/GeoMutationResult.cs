namespace LdprActivistDemo.Application.Geo.Models;

public sealed record GeoMutationResult<T>(bool IsSuccess, GeoMutationError Error, T? Value)
{
	public static GeoMutationResult<T> Ok(T value) => new(true, GeoMutationError.None, value);
	public static GeoMutationResult<T> Fail(GeoMutationError error) => new(false, error, default);
}