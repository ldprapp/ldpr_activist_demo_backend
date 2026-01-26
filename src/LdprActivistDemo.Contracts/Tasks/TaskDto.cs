namespace LdprActivistDemo.Contracts.Tasks;

public sealed record TaskDto(
	Guid Id,
	Guid AuthorUserId,
	string Title,
	string Description,
	string? RequirementsText,
	int RewardPoints,
	string? CoverImageUrl,
	string? ExecutionLocation,
	DateTimeOffset PublishedAt,
	DateTimeOffset? DeadlineAt,
	TaskStatus Status,
	int RegionId,
	int? CityId,
	IReadOnlyList<Guid> TrustedAdminIds);