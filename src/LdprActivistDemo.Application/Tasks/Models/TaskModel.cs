namespace LdprActivistDemo.Application.Tasks.Models;

public sealed record TaskModel(
	Guid Id,
	Guid AuthorUserId,
	string Title,
	string Description,
	string RequirementsText,
	int RewardPoints,
	Guid? CoverImageId,
	string? ExecutionLocation,
	DateTimeOffset PublishedAt,
	DateTimeOffset DeadlineAt,
	string Status,
	string RegionName,
	string? CityName,
	IReadOnlyList<Guid> TrustedAdminIds,
	string VerificationType,
	string ReuseType,
	string? AutoVerificationActionType);