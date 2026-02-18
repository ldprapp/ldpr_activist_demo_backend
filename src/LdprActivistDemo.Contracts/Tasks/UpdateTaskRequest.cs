using System.ComponentModel.DataAnnotations;

namespace LdprActivistDemo.Contracts.Tasks;

public sealed record UpdateTaskRequest(
	[Required] string Title,
	[Required] string Description,
	string? RequirementsText,
	[Range(0, int.MaxValue)] int RewardPoints,
	Guid? CoverImageId,
	string? ExecutionLocation,
	DateTimeOffset PublishedAt,
	DateTimeOffset? DeadlineAt,
	string Status,
	[Range(1, int.MaxValue)] int RegionId,
	int? CityId,
	IReadOnlyList<Guid>? TrustedAdminIds);