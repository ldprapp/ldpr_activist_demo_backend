namespace LdprActivistDemo.Persistence;

public sealed class TaskSubmissionImage
{
	public Guid SubmissionId { get; set; }

	public TaskSubmission Submission { get; set; } = null!;

	public Guid ImageId { get; set; }

	public ImageEntity Image { get; set; } = null!;
}