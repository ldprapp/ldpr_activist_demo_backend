namespace LdprActivistDemo.Contracts.UserPoints;

public sealed record UserPointsTransactionDto(
	Guid Id,
	int Amount,
	DateTimeOffset TransactionAt,
	string Comment);