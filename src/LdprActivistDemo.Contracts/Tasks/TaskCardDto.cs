namespace LdprActivistDemo.Contracts.Tasks;

public sealed record TaskCardDto(
	Guid Id,
	string Title,
	string Description,
	int RewardPoints,
	Guid? CoverImageId,
	DateTimeOffset? DeadlineAt,
	TaskStatus Status,
	int RegionId,
	int? CityId);