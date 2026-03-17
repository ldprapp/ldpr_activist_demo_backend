using LdprActivistDemo.Application.UserPoints.Models;
using LdprActivistDemo.Application.Users;
using LdprActivistDemo.Application.Users.Models;

namespace LdprActivistDemo.Application.UserPoints;

public sealed class UserPointsService : IUserPointsService
{
	private readonly IUserRepository _users;
	private readonly IUserPointsTransactionRepository _transactions;
	private readonly IActorAccessService _actorAccess;

	public UserPointsService(IUserRepository users, IUserPointsTransactionRepository transactions, IActorAccessService actorAccess)
	{
		_users = users ?? throw new ArgumentNullException(nameof(users));
		_transactions = transactions ?? throw new ArgumentNullException(nameof(transactions));
		_actorAccess = actorAccess ?? throw new ArgumentNullException(nameof(actorAccess));
	}

	public async Task<UserPointsResult<int>> GetBalanceAsync(
		Guid actorUserId,
		string actorUserPassword,
		Guid userId,
		CancellationToken cancellationToken)
	{
		if(actorUserId == Guid.Empty
		   || userId == Guid.Empty
		   || string.IsNullOrWhiteSpace(actorUserPassword))
		{
			return UserPointsResult<int>.Fail(UserPointsError.ValidationFailed);
		}

		var actorAuth = await _actorAccess.AuthenticateAsync(actorUserId, actorUserPassword, cancellationToken);
		if(!actorAuth.IsSuccess)
		{
			return UserPointsResult<int>.Fail(UserPointsError.InvalidCredentials);
		}

		var actor = actorAuth.Actor!;
		if(actorUserId != userId)
		{
			if(!UserRoleRules.HasCoordinatorAccess(actor.Role))
			{
				return UserPointsResult<int>.Fail(UserPointsError.Forbidden);
			}
		}

		var balance = await _transactions.GetBalanceAsync(userId, cancellationToken);
		return balance is null
			? UserPointsResult<int>.Fail(UserPointsError.UserNotFound)
			: UserPointsResult<int>.Ok(balance.Value);
	}

	public async Task<UserPointsResult<IReadOnlyList<UserPointsTransactionModel>>> GetTransactionsAsync(
		Guid actorUserId,
		string actorUserPassword,
		Guid userId,
		CancellationToken cancellationToken)
	{
		if(actorUserId == Guid.Empty
		   || userId == Guid.Empty
		   || string.IsNullOrWhiteSpace(actorUserPassword))
		{
			return UserPointsResult<IReadOnlyList<UserPointsTransactionModel>>.Fail(UserPointsError.ValidationFailed);
		}

		var actorAuth = await _actorAccess.AuthenticateAsync(actorUserId, actorUserPassword, cancellationToken);
		if(!actorAuth.IsSuccess)
		{
			return UserPointsResult<IReadOnlyList<UserPointsTransactionModel>>.Fail(UserPointsError.InvalidCredentials);
		}

		var actor = actorAuth.Actor!;
		if(actorUserId != userId)
		{
			if(!UserRoleRules.HasCoordinatorAccess(actor.Role))
			{
				return UserPointsResult<IReadOnlyList<UserPointsTransactionModel>>.Fail(UserPointsError.Forbidden);
			}
		}

		var list = await _transactions.GetTransactionsAsync(userId, excludeInitialization: true, cancellationToken);
		return list is null
			? UserPointsResult<IReadOnlyList<UserPointsTransactionModel>>.Fail(UserPointsError.UserNotFound)
			: UserPointsResult<IReadOnlyList<UserPointsTransactionModel>>.Ok(list);
	}

	public async Task<UserPointsResult<Guid>> CreateTransactionAsync(
		Guid actorUserId,
		string actorUserPassword,
		Guid userId,
		int amount,
		string comment,
		Guid? coordinatorUserId,
		Guid? taskId,
		CancellationToken cancellationToken)
	{
		comment = (comment ?? string.Empty).Trim();
		coordinatorUserId = coordinatorUserId.HasValue && coordinatorUserId.Value == Guid.Empty
			? null
			: coordinatorUserId;
		taskId = taskId.HasValue && taskId.Value == Guid.Empty
			? null
			: taskId;

		if(actorUserId == Guid.Empty
		   || userId == Guid.Empty
		   || string.IsNullOrWhiteSpace(actorUserPassword)
		   || amount == 0
		   || comment.Length == 0)
		{
			return UserPointsResult<Guid>.Fail(UserPointsError.ValidationFailed);
		}

		if(!taskId.HasValue && !coordinatorUserId.HasValue)
		{
			return UserPointsResult<Guid>.Fail(UserPointsError.ValidationFailed);
		}

		if(coordinatorUserId.HasValue && coordinatorUserId.Value != actorUserId)
		{
			return UserPointsResult<Guid>.Fail(UserPointsError.ValidationFailed);
		}

		var actorAuth = await _actorAccess.AuthenticateAsync(actorUserId, actorUserPassword, cancellationToken);
		if(!actorAuth.IsSuccess)
		{
			return UserPointsResult<Guid>.Fail(UserPointsError.InvalidCredentials);
		}

		var actor = actorAuth.Actor!;
		if(!UserRoleRules.HasCoordinatorAccess(actor.Role))
		{
			return UserPointsResult<Guid>.Fail(UserPointsError.Forbidden);
		}

		var currentBalance = await _transactions.GetBalanceAsync(userId, cancellationToken);
		if(currentBalance is null)
		{
			return UserPointsResult<Guid>.Fail(UserPointsError.UserNotFound);
		}

		var nextBalance = currentBalance.Value + amount;
		if(nextBalance < 0)
		{
			return UserPointsResult<Guid>.Fail(UserPointsError.InsufficientBalance);
		}

		var id = await _transactions.CreateAsync(
			userId,
			amount,
			comment,
			DateTimeOffset.UtcNow,
			coordinatorUserId,
			taskId,
			cancellationToken);

		return id is null
			? UserPointsResult<Guid>.Fail(UserPointsError.UserNotFound)
			: UserPointsResult<Guid>.Ok(id.Value);
	}
}