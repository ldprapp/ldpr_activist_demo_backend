namespace LdprActivistDemo.Contracts.UserPoints;

public sealed record UserPointsTransactionDto(
	Guid Id,
	int Amount,
	DateTimeOffset TransactionAt,
	string Comment,
	Guid? CoordinatorUserId,
	Guid? TaskId,
	bool IsCancelled,
	string CancellationComment,
	DateTimeOffset? CancelledAtUtc,
	Guid? CancelledByAdminUserId);