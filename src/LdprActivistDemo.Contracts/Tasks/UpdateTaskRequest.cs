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
	[Required] string RegionName,
	string? SettlementName,
	IReadOnlyList<Guid>? TrustedCoordinatorIds,
	string? VerificationType = null,
	string? ReuseType = null,
	string? AutoVerificationActionType = null);