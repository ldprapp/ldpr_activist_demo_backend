namespace LdprActivistDemo.Application.Tasks.Models;

public sealed record TaskCreateModel(
	string Title,
	string Description,
	string? RequirementsText,
	int RewardPoints,
	Guid? CoverImageId,
	string? ExecutionLocation,
	DateTimeOffset PublishedAt,
	DateTimeOffset? DeadlineAt,
	int RegionId,
	int? CityId,
	IReadOnlyList<Guid> TrustedAdminIds,
	string VerificationType);