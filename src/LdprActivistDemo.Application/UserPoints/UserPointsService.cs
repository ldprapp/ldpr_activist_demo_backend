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
		var auth = await AuthorizeReadAsync(actorUserId, actorUserPassword, userId, cancellationToken);
		if(auth.Error is not null)
		{
			return UserPointsResult<int>.Fail(auth.Error.Value);
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
		var auth = await AuthorizeReadAsync(actorUserId, actorUserPassword, userId, cancellationToken);
		if(auth.Error is not null)
		{
			return UserPointsResult<IReadOnlyList<UserPointsTransactionModel>>.Fail(auth.Error.Value);
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

		if(userId == Guid.Empty || amount == 0 || comment.Length == 0)
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

		var auth = await AuthorizeCoordinatorWriteAsync(actorUserId, actorUserPassword, userId, cancellationToken);
		if(auth.Error is not null)
		{
			return UserPointsResult<Guid>.Fail(auth.Error.Value);
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

	private async Task<(UserPointsError? Error, UserInternalModel? Actor)> AuthorizeReadAsync(
		Guid actorUserId,
		string actorUserPassword,
		Guid userId,
		CancellationToken cancellationToken)
	{
		if(userId == Guid.Empty)
		{
			return (UserPointsError.ValidationFailed, null);
		}

		var auth = await AuthenticateAsync(actorUserId, actorUserPassword, cancellationToken);
		if(auth.Error is not null)
		{
			return auth;
		}

		if(actorUserId != userId && !UserRoleRules.HasCoordinatorAccess(auth.Actor!.Role))
		{
			return (UserPointsError.Forbidden, null);
		}

		return auth;
	}

	private async Task<(UserPointsError? Error, UserInternalModel? Actor)> AuthorizeCoordinatorWriteAsync(
		Guid actorUserId,
		string actorUserPassword,
		Guid userId,
		CancellationToken cancellationToken)
	{
		if(userId == Guid.Empty)
		{
			return (UserPointsError.ValidationFailed, null);
		}

		var auth = await AuthenticateAsync(actorUserId, actorUserPassword, cancellationToken);
		if(auth.Error is not null)
		{
			return auth;
		}

		if(!UserRoleRules.HasCoordinatorAccess(auth.Actor!.Role))
		{
			return (UserPointsError.Forbidden, null);
		}

		return auth;
	}

	private async Task<(UserPointsError? Error, UserInternalModel? Actor)> AuthenticateAsync(
		Guid actorUserId,
		string actorUserPassword,
		CancellationToken cancellationToken)
	{
		if(actorUserId == Guid.Empty || string.IsNullOrWhiteSpace(actorUserPassword))
		{
			return (UserPointsError.ValidationFailed, null);
		}

		var actorAuth = await _actorAccess.AuthenticateAsync(actorUserId, actorUserPassword, cancellationToken);
		if(!actorAuth.IsSuccess)
		{
			return (UserPointsError.InvalidCredentials, null);
		}

		return (null, actorAuth.Actor);
	}
}