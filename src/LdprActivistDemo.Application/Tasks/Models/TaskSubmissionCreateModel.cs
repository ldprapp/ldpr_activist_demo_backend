namespace LdprActivistDemo.Application.Tasks.Models;

public sealed record TaskSubmissionCreateModel(
	IReadOnlyList<string>? PhotoUrls,
	string? ProofText,
	DateTimeOffset SubmittedAt);