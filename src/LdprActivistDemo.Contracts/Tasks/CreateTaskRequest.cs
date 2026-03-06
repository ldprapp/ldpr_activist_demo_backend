using System.ComponentModel.DataAnnotations;

namespace LdprActivistDemo.Contracts.Tasks;

public sealed record CreateTaskRequest(
	[Required] string Title,
	[Required] string Description,
	string? RequirementsText,
	[Range(0, int.MaxValue)] int RewardPoints,
	Guid? CoverImageId,
	string? ExecutionLocation,
	DateTimeOffset PublishedAt,
	DateTimeOffset? DeadlineAt,
	[Required] string RegionName,
	string? CityName,
	IReadOnlyList<Guid>? TrustedAdminIds,
	string VerificationType = TaskVerificationType.Manual,
	string ReuseType = TaskReuseType.Disposable,
	string? AutoVerificationActionType = null);