namespace LdprActivistDemo.Application.Images.Models;

public sealed record SystemImageStorageUpsertResult(
	bool IsCreated,
	SystemImageModel Value);