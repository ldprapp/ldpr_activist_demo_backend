namespace LdprActivistDemo.Application.Images.Models;

public enum ImageDeleteError
{
	None = 0,
	ValidationFailed = 1,
	InvalidCredentials = 2,
	Forbidden = 3,
	ImageNotFound = 4,
}

public readonly record struct ImageDeleteResult(ImageDeleteError Error)
{
	public bool IsSuccess => Error == ImageDeleteError.None;

	public static ImageDeleteResult Success()
		=> new(ImageDeleteError.None);

	public static ImageDeleteResult Fail(ImageDeleteError error)
		=> new(error);
}