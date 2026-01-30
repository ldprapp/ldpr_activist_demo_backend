namespace LdprActivistDemo.Contracts.Tasks;

public sealed record SubmitTaskRequest(
	DateTimeOffset SubmittedAt,
	IReadOnlyList<Guid>? PhotoImageIds,
	string? ProofText);