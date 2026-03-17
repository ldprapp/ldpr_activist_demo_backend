namespace LdprActivistDemo.Application.Tasks.Models;

public sealed record TaskUpdateModel(
	string Title,
	string Description,
	string? RequirementsText,
	int RewardPoints,
	Guid? CoverImageId,
	string? ExecutionLocation,
	DateTimeOffset PublishedAt,
	DateTimeOffset? DeadlineAt,
	string RegionName,
	string? CityName,
	IReadOnlyList<Guid> TrustedCoordinatorIds,
	string? VerificationType,
	string? ReuseType,
	string? AutoVerificationActionType);