namespace LdprActivistDemo.Persistence;

public sealed class TaskSubmission
{
	public Guid Id { get; set; }

	public Guid TaskId { get; set; }

	public TaskEntity Task { get; set; } = null!;

	public Guid UserId { get; set; }

	public User User { get; set; } = null!;

	public DateTimeOffset SubmittedAt { get; set; }

	public Guid? ConfirmedByAdminId { get; set; }

	public User? ConfirmedByAdmin { get; set; }

	public DateTimeOffset? ConfirmedAt { get; set; }

	public List<TaskSubmissionImage> PhotoImages { get; set; } = new();

	public string? ProofText { get; set; }
}