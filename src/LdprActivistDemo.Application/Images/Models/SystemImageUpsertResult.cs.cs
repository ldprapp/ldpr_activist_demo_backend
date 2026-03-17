namespace LdprActivistDemo.Application.Images.Models;

public enum SystemImageUpsertError
{
	None = 0,
	ValidationFailed = 1,
	InvalidCredentials = 2,
	Forbidden = 3,
}

public sealed record SystemImageUpsertResult(bool IsSuccess, SystemImageUpsertError Error, bool IsCreated, SystemImageModel? Value)
{
	public static SystemImageUpsertResult Created(SystemImageModel value)
		=> new(true, SystemImageUpsertError.None, true, value);

	public static SystemImageUpsertResult Updated(SystemImageModel value)
		=> new(true, SystemImageUpsertError.None, false, value);

	public static SystemImageUpsertResult Fail(SystemImageUpsertError error)
		=> new(false, error, false, null);
}