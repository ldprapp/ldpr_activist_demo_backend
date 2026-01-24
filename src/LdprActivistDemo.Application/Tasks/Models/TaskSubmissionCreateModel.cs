namespace LdprActivistDemo.Application.Tasks.Models;

public sealed record TaskSubmissionCreateModel(
	Guid UserId,
	string UserPasswordHash,
	Guid TaskId,
	IReadOnlyList<string>? PhotoUrls,
	string? ProofText,
	DateTimeOffset SubmittedAt);