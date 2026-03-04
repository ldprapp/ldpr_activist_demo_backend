namespace LdprActivistDemo.Application.Tasks.Models;

public sealed record TaskSubmissionModel(
	Guid Id,
	Guid TaskId,
	Guid UserId,
	DateTimeOffset SubmittedAt,
	string DecisionStatus,
	Guid? DecidedByAdminId,
	DateTimeOffset? DecidedAt,
	IReadOnlyList<Guid>? PhotoImageIds,
	string? ProofText);