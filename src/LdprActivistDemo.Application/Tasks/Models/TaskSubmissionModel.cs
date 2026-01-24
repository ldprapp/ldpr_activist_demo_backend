namespace LdprActivistDemo.Application.Tasks.Models;

public sealed record TaskSubmissionModel(
	Guid Id,
	Guid TaskId,
	Guid UserId,
	DateTimeOffset SubmittedAt,
	Guid? ConfirmedByAdminId,
	DateTimeOffset? ConfirmedAt,
	IReadOnlyList<string>? PhotoUrls,
	string? ProofText);