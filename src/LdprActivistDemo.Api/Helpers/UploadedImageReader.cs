using LdprActivistDemo.Application.Images.Models;

namespace LdprActivistDemo.Api.Helpers;

public static class UploadedImageReader
{
	public const long DefaultMaxImageBytes = 10 * 1024 * 1024;

	public static string? ValidateImage(IFormFile file, long maxBytes = DefaultMaxImageBytes)
	{
		if(file is null)
		{
			return "File is required.";
		}

		if(file.Length <= 0)
		{
			return "File is empty.";
		}

		if(file.Length > maxBytes)
		{
			return $"File is too large. Max allowed is {maxBytes} bytes.";
		}

		var ct = (file.ContentType ?? string.Empty).Trim();
		if(ct.Length == 0 || !ct.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
		{
			return "Only image/* content types are allowed.";
		}

		return null;
	}

	public static async Task<ImageCreateModel> ReadAsync(IFormFile file, CancellationToken cancellationToken)
	{
		var contentType = string.IsNullOrWhiteSpace(file.ContentType)
			? "application/octet-stream"
			: file.ContentType.Trim();

		await using var ms = new MemoryStream(capacity: (int)Math.Min(file.Length, int.MaxValue));
		await file.CopyToAsync(ms, cancellationToken);

		return new ImageCreateModel(contentType, ms.ToArray());
	}
}