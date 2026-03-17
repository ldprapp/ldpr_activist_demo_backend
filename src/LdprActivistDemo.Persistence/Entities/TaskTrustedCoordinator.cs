namespace LdprActivistDemo.Persistence;

public sealed class TaskTrustedCoordinator
{
	public Guid TaskId { get; set; }
	public TaskEntity Task { get; set; } = null!;
	public Guid CoordinatorUserId { get; set; }
	public User CoordinatorUser { get; set; } = null!;
}