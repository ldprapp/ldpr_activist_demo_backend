using LdprActivistDemo.Application.UserPoints.Models;

namespace LdprActivistDemo.Application.UserPoints;

public interface IUserPointsService
{
	Task<UserPointsResult<int>> GetBalanceAsync(
		Guid actorUserId,
		string actorUserPassword,
		Guid userId,
		CancellationToken cancellationToken);

	Task<UserPointsResult<IReadOnlyList<UserPointsTransactionModel>>> GetTransactionsAsync(
		Guid actorUserId,
		string actorUserPassword,
		Guid userId,
		CancellationToken cancellationToken);

	Task<UserPointsResult<Guid>> CreateTransactionAsync(
		Guid actorUserId,
		string actorUserPassword,
		Guid userId,
		int amount,
		string comment,
		CancellationToken cancellationToken);
}