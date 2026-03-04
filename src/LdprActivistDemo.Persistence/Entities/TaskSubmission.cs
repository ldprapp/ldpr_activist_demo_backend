using LdprActivistDemo.Contracts.Tasks;

namespace LdprActivistDemo.Persistence;

public sealed class TaskSubmission
{
	public Guid Id { get; set; }

	public Guid TaskId { get; set; }

	public TaskEntity Task { get; set; } = null!;

	public Guid UserId { get; set; }

	public User User { get; set; } = null!;

	public DateTimeOffset SubmittedAt { get; set; }

	public string DecisionStatus { get; set; } = TaskSubmissionDecisionStatus.InProgress;

	public Guid? DecidedByAdminId { get; set; }

	public User? DecidedByAdmin { get; set; }

	public DateTimeOffset? DecidedAt { get; set; }

	public List<TaskSubmissionImage> PhotoImages { get; set; } = new();

	public string? ProofText { get; set; }
}