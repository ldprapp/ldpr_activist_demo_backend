using LdprActivistDemo.Application.UserPoints.Models;

namespace LdprActivistDemo.Application.UserPoints;

public interface IUserPointsTransactionRepository
{
	Task<int?> GetBalanceAsync(Guid userId, CancellationToken cancellationToken);

	Task<IReadOnlyList<UserPointsTransactionModel>?> GetTransactionsAsync(
		Guid userId,
		bool excludeInitialization,
		CancellationToken cancellationToken);

	Task<Guid?> CreateAsync(
		Guid userId,
		int amount,
		string comment,
		DateTimeOffset transactionAtUtc,
		Guid? coordinatorUserId,
		Guid? taskId,
		CancellationToken cancellationToken);

	Task<bool> CancelAsync(
		Guid transactionId,
		string cancellationComment,
		Guid cancelledByAdminUserId,
		DateTimeOffset cancelledAtUtc,
		CancellationToken cancellationToken);

	Task<bool> RestoreAsync(
		Guid transactionId,
		CancellationToken cancellationToken);
}