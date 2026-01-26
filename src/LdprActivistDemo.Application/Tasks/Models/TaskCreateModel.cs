using TaskStatus = LdprActivistDemo.Contracts.Tasks.TaskStatus;

namespace LdprActivistDemo.Application.Tasks.Models;

public sealed record TaskCreateModel(
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