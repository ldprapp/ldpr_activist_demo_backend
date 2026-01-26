namespace LdprActivistDemo.Contracts.Tasks;

public sealed record SubmitTaskRequest(
	DateTimeOffset SubmittedAt,
	IReadOnlyList<string>? PhotoUrls,
	string? ProofText);