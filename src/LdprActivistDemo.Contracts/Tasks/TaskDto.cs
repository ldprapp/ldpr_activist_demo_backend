namespace LdprActivistDemo.Contracts.Tasks;

public sealed record TaskDto(
	Guid Id,
	Guid AuthorUserId,
	string Title,
	string Description,
	string? RequirementsText,
	int RewardPoints,
	Guid? CoverImageId,
	string? ExecutionLocation,
	DateTimeOffset PublishedAt,
	DateTimeOffset? DeadlineAt,
	string Status,
	int RegionId,
	int? CityId,
	IReadOnlyList<Guid> TrustedAdminIds,
	string VerificationType = TaskVerificationType.Manual,
	string ReuseType = TaskReuseType.Disposable,
	string? AutoVerificationActionType = null);