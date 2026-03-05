namespace LdprActivistDemo.Application.UserPoints.Models;

public sealed record UserPointsTransactionModel(
	Guid Id,
	Guid UserId,
	int Amount,
	DateTimeOffset TransactionAt,
	string Comment);