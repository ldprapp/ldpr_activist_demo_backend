namespace LdprActivistDemo.Contracts.Tasks;

public sealed record SubmissionDto(
	Guid Id,
	Guid TaskId,
	Guid UserId,
	DateTimeOffset SubmittedAt,
	string? DecisionStatus,
	Guid? DecidedByAdminId,
	DateTimeOffset? DecidedAt,
	IReadOnlyList<Guid>? PhotoImageIds,
	string? ProofText);