namespace LdprActivistDemo.Persistence;

public sealed class UserPointsTransaction
{
	public Guid Id { get; set; }

	public Guid UserId { get; set; }
	public User User { get; set; } = null!;

	public int Amount { get; set; }

	public DateTimeOffset TransactionAt { get; set; }

	public string Comment { get; set; } = string.Empty;

	public Guid? CoordinatorUserId { get; set; }
	public User? CoordinatorUser { get; set; }

	public Guid? TaskId { get; set; }
	public TaskEntity? Task { get; set; }
}