using System.ComponentModel.DataAnnotations;

namespace LdprActivistDemo.Contracts.Tasks;

public sealed record CreateTaskRequest(
	[Required] string Title,
	[Required] string Description,
	string? RequirementsText,
	[Range(0, int.MaxValue)] int RewardPoints,
	string? CoverImageUrl,
	string? ExecutionLocation,
	DateTimeOffset PublishedAt,
	DateTimeOffset? DeadlineAt,
	TaskStatus Status,
	[Range(1, int.MaxValue)] int RegionId,
	int? CityId,
	IReadOnlyList<Guid>? TrustedAdminIds);