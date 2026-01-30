namespace LdprActivistDemo.Contracts.Tasks;

public sealed record SubmissionDto(
	Guid Id,
	Guid TaskId,
	Guid UserId,
	DateTimeOffset SubmittedAt,
	Guid? ConfirmedByAdminId,
	DateTimeOffset? ConfirmedAt,
	IReadOnlyList<Guid>? PhotoImageIds,
	string? ProofText);