namespace LdprActivistDemo.Application.Geo.Models;

public readonly record struct GeoMutationResult(GeoMutationError Error)
{
	public bool IsSuccess => Error == GeoMutationError.None;

	public static GeoMutationResult Success() => new(GeoMutationError.None);
	public static GeoMutationResult Fail(GeoMutationError error) => new(error);
}

public sealed record GeoMutationResult<T>(bool IsSuccess, GeoMutationError Error, T? Value)
{
	public static GeoMutationResult<T> Ok(T value) => new(true, GeoMutationError.None, value);
	public static GeoMutationResult<T> Fail(GeoMutationError error) => new(false, error, default);
}