namespace LdprActivistDemo.Persistence;

public sealed class TaskTrustedAdmin
{
	public Guid TaskId { get; set; }

	public TaskEntity Task { get; set; } = null!;

	public Guid AdminUserId { get; set; }

	public User AdminUser { get; set; } = null!;
}