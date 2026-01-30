namespace LdprActivistDemo.Application.Tasks.Models;

public sealed record TaskSubmissionCreateModel(
	IReadOnlyList<Guid>? PhotoImageIds,
	string? ProofText,
	DateTimeOffset SubmittedAt);