using LdprActivistDemo.Application.Diagnostics;
using LdprActivistDemo.Application.Logging;
using LdprActivistDemo.Application.UserPoints.Models;
using LdprActivistDemo.Application.Users;
using LdprActivistDemo.Application.Users.Models;

using Microsoft.Extensions.Logging;

namespace LdprActivistDemo.Application.UserPoints;

public sealed class UserPointsService : IUserPointsService
{
	private readonly IUserRepository _users;
	private readonly IUserPointsTransactionRepository _transactions;
	private readonly IActorAccessService _actorAccess;
	private readonly ILogger<UserPointsService> _logger;

	public UserPointsService(
		IUserRepository users,
		IUserPointsTransactionRepository transactions,
		IActorAccessService actorAccess,
		ILogger<UserPointsService> logger)
	{
		_users = users ?? throw new ArgumentNullException(nameof(users));
		_transactions = transactions ?? throw new ArgumentNullException(nameof(transactions));
		_actorAccess = actorAccess ?? throw new ArgumentNullException(nameof(actorAccess));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	public async Task<UserPointsResult<int>> GetBalanceAsync(
		Guid userId,
		CancellationToken cancellationToken)
	{
		var properties = new (string Name, object? Value)[]
		{
			("UserId", userId),
		};

		using var scope = _logger.BeginExecutionScope(
			DomainLogEvents.UserPoints.GetBalance,
			LogLayers.ApplicationService,
			ApplicationLogOperations.UserPoints.GetBalance,
			properties);

		_logger.LogStarted(
			DomainLogEvents.UserPoints.GetBalance,
			LogLayers.ApplicationService,
			ApplicationLogOperations.UserPoints.GetBalance,
			"User points balance read started.",
			properties);

		try
		{
			if(userId == Guid.Empty)
			{
				_logger.LogRejected(
					LogLevel.Warning,
					DomainLogEvents.UserPoints.GetBalance,
					LogLayers.ApplicationService,
					ApplicationLogOperations.UserPoints.GetBalance,
					"User points balance read rejected by validation.",
					StructuredLog.Combine(properties, ("Error", UserPointsError.ValidationFailed)));

				return UserPointsResult<int>.Fail(UserPointsError.ValidationFailed);
			}

			var balance = await _transactions.GetBalanceAsync(userId, cancellationToken);
			if(balance is null)
			{
				_logger.LogRejected(
					LogLevel.Warning,
					DomainLogEvents.UserPoints.GetBalance,
					LogLayers.ApplicationService,
					ApplicationLogOperations.UserPoints.GetBalance,
					"User points balance read rejected. Target user not found.",
					StructuredLog.Combine(properties, ("Error", UserPointsError.UserNotFound)));

				return UserPointsResult<int>.Fail(UserPointsError.UserNotFound);
			}

			_logger.LogCompleted(
				LogLevel.Debug,
				DomainLogEvents.UserPoints.GetBalance,
				LogLayers.ApplicationService,
				ApplicationLogOperations.UserPoints.GetBalance,
				"User points balance read completed.",
				StructuredLog.Combine(properties, ("Balance", balance.Value)));

			return UserPointsResult<int>.Ok(balance.Value);
		}
		catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
		{
			_logger.LogAborted(
				LogLevel.Information,
				DomainLogEvents.UserPoints.GetBalance,
				LogLayers.ApplicationService,
				ApplicationLogOperations.UserPoints.GetBalance,
				"User points balance read aborted.",
				properties);
			throw;
		}
		catch(Exception ex)
		{
			_logger.LogFailed(
				LogLevel.Error,
				DomainLogEvents.UserPoints.GetBalance,
				LogLayers.ApplicationService,
				ApplicationLogOperations.UserPoints.GetBalance,
				"User points balance read failed.",
				ex,
				properties);
			throw;
		}
	}

	public async Task<UserPointsResult<IReadOnlyList<UserPointsTransactionModel>>> GetTransactionsAsync(
		Guid actorUserId,
		string actorUserPassword,
		Guid userId,
		CancellationToken cancellationToken)
	{
		var properties = new (string Name, object? Value)[]
		{
			("ActorUserId", actorUserId),
			("UserId", userId),
		};

		using var scope = _logger.BeginExecutionScope(
			DomainLogEvents.UserPoints.GetTransactions,
			LogLayers.ApplicationService,
			ApplicationLogOperations.UserPoints.GetTransactions,
			properties);

		_logger.LogStarted(
			DomainLogEvents.UserPoints.GetTransactions,
			LogLayers.ApplicationService,
			ApplicationLogOperations.UserPoints.GetTransactions,
			"User points transactions read started.",
			properties);

		try
		{
			var auth = await AuthorizeSelfOrAdminReadAsync(actorUserId, actorUserPassword, userId, cancellationToken);
			if(auth.Error is not null)
			{
				_logger.LogRejected(
					LogLevel.Warning,
					DomainLogEvents.UserPoints.GetTransactions,
					LogLayers.ApplicationService,
					ApplicationLogOperations.UserPoints.GetTransactions,
					"User points transactions read rejected.",
					StructuredLog.Combine(properties, ("Error", auth.Error.Value)));

				return UserPointsResult<IReadOnlyList<UserPointsTransactionModel>>.Fail(auth.Error.Value);
			}

			var list = await _transactions.GetTransactionsAsync(userId, excludeInitialization: true, cancellationToken);
			if(list is null)
			{
				_logger.LogRejected(
					LogLevel.Warning,
					DomainLogEvents.UserPoints.GetTransactions,
					LogLayers.ApplicationService,
					ApplicationLogOperations.UserPoints.GetTransactions,
					"User points transactions read rejected. Target user not found.",
					StructuredLog.Combine(properties, ("Error", UserPointsError.UserNotFound)));

				return UserPointsResult<IReadOnlyList<UserPointsTransactionModel>>.Fail(UserPointsError.UserNotFound);
			}

			_logger.LogCompleted(
				LogLevel.Debug,
				DomainLogEvents.UserPoints.GetTransactions,
				LogLayers.ApplicationService,
				ApplicationLogOperations.UserPoints.GetTransactions,
				"User points transactions read completed.",
				StructuredLog.Combine(properties, ("Count", list.Count)));

			return UserPointsResult<IReadOnlyList<UserPointsTransactionModel>>.Ok(list);
		}
		catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
		{
			_logger.LogAborted(
				LogLevel.Information,
				DomainLogEvents.UserPoints.GetTransactions,
				LogLayers.ApplicationService,
				ApplicationLogOperations.UserPoints.GetTransactions,
				"User points transactions read aborted.",
				properties);
			throw;
		}
		catch(Exception ex)
		{
			_logger.LogFailed(
				LogLevel.Error,
				DomainLogEvents.UserPoints.GetTransactions,
				LogLayers.ApplicationService,
				ApplicationLogOperations.UserPoints.GetTransactions,
				"User points transactions read failed.",
				ex,
				properties);
			throw;
		}
	}

	public async Task<UserPointsResult<bool>> CancelTransactionAsync(
		Guid actorUserId,
		string actorUserPassword,
		Guid transactionId,
		string cancellationComment,
		CancellationToken cancellationToken)
	{
		cancellationComment = (cancellationComment ?? string.Empty).Trim();

		var properties = new (string Name, object? Value)[]
		{
			("ActorUserId", actorUserId),
			("TransactionId", transactionId),
		};

		using var scope = _logger.BeginExecutionScope(
			DomainLogEvents.UserPoints.CancelTransaction,
			LogLayers.ApplicationService,
			ApplicationLogOperations.UserPoints.CancelTransaction,
			properties);

		_logger.LogStarted(
			DomainLogEvents.UserPoints.CancelTransaction,
			LogLayers.ApplicationService,
			ApplicationLogOperations.UserPoints.CancelTransaction,
			"User points transaction cancel started.",
			properties);

		try
		{
			if(transactionId == Guid.Empty || cancellationComment.Length == 0)
			{
				_logger.LogRejected(
					LogLevel.Warning,
					DomainLogEvents.UserPoints.CancelTransaction,
					LogLayers.ApplicationService,
					ApplicationLogOperations.UserPoints.CancelTransaction,
					"User points transaction cancel rejected by validation.",
					StructuredLog.Combine(properties, ("Error", UserPointsError.ValidationFailed)));

				return UserPointsResult<bool>.Fail(UserPointsError.ValidationFailed);
			}

			var auth = await AuthorizeAdminAsync(actorUserId, actorUserPassword, cancellationToken);
			if(auth.Error is not null)
			{
				_logger.LogRejected(
					LogLevel.Warning,
					DomainLogEvents.UserPoints.CancelTransaction,
					LogLayers.ApplicationService,
					ApplicationLogOperations.UserPoints.CancelTransaction,
					"User points transaction cancel rejected.",
					StructuredLog.Combine(properties, ("Error", auth.Error.Value)));

				return UserPointsResult<bool>.Fail(auth.Error.Value);
			}

			var result = await _transactions.CancelAsync(
				transactionId,
				cancellationComment,
				actorUserId,
				DateTimeOffset.UtcNow,
				cancellationToken);

			if(!result)
			{
				_logger.LogRejected(
					LogLevel.Warning,
					DomainLogEvents.UserPoints.CancelTransaction,
					LogLayers.ApplicationService,
					ApplicationLogOperations.UserPoints.CancelTransaction,
					"User points transaction cancel rejected. Transaction not found.",
					StructuredLog.Combine(properties, ("Error", UserPointsError.TransactionNotFound)));

				return UserPointsResult<bool>.Fail(UserPointsError.TransactionNotFound);
			}

			_logger.LogCompleted(
				LogLevel.Information,
				DomainLogEvents.UserPoints.CancelTransaction,
				LogLayers.ApplicationService,
				ApplicationLogOperations.UserPoints.CancelTransaction,
				"User points transaction cancelled.",
				properties);

			return UserPointsResult<bool>.Ok(true);
		}
		catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
		{
			_logger.LogAborted(
				LogLevel.Information,
				DomainLogEvents.UserPoints.CancelTransaction,
				LogLayers.ApplicationService,
				ApplicationLogOperations.UserPoints.CancelTransaction,
				"User points transaction cancel aborted.",
				properties);
			throw;
		}
		catch(Exception ex)
		{
			_logger.LogFailed(
				LogLevel.Error,
				DomainLogEvents.UserPoints.CancelTransaction,
				LogLayers.ApplicationService,
				ApplicationLogOperations.UserPoints.CancelTransaction,
				"User points transaction cancel failed.",
				ex,
				properties);
			throw;
		}
	}

	public async Task<UserPointsResult<bool>> RestoreTransactionAsync(
		Guid actorUserId,
		string actorUserPassword,
		Guid transactionId,
		CancellationToken cancellationToken)
	{
		var properties = new (string Name, object? Value)[]
		{
			("ActorUserId", actorUserId),
			("TransactionId", transactionId),
		};

		using var scope = _logger.BeginExecutionScope(
			DomainLogEvents.UserPoints.RestoreTransaction,
			LogLayers.ApplicationService,
			ApplicationLogOperations.UserPoints.RestoreTransaction,
			properties);

		_logger.LogStarted(
			DomainLogEvents.UserPoints.RestoreTransaction,
			LogLayers.ApplicationService,
			ApplicationLogOperations.UserPoints.RestoreTransaction,
			"User points transaction restore started.",
			properties);

		try
		{
			if(transactionId == Guid.Empty)
			{
				_logger.LogRejected(
					LogLevel.Warning,
					DomainLogEvents.UserPoints.RestoreTransaction,
					LogLayers.ApplicationService,
					ApplicationLogOperations.UserPoints.RestoreTransaction,
					"User points transaction restore rejected by validation.",
					StructuredLog.Combine(properties, ("Error", UserPointsError.ValidationFailed)));

				return UserPointsResult<bool>.Fail(UserPointsError.ValidationFailed);
			}

			var auth = await AuthorizeAdminAsync(actorUserId, actorUserPassword, cancellationToken);
			if(auth.Error is not null)
			{
				_logger.LogRejected(
					LogLevel.Warning,
					DomainLogEvents.UserPoints.RestoreTransaction,
					LogLayers.ApplicationService,
					ApplicationLogOperations.UserPoints.RestoreTransaction,
					"User points transaction restore rejected.",
					StructuredLog.Combine(properties, ("Error", auth.Error.Value)));

				return UserPointsResult<bool>.Fail(auth.Error.Value);
			}

			var result = await _transactions.RestoreAsync(
				transactionId,
				cancellationToken);

			if(!result)
			{
				_logger.LogRejected(
					LogLevel.Warning,
					DomainLogEvents.UserPoints.RestoreTransaction,
					LogLayers.ApplicationService,
					ApplicationLogOperations.UserPoints.RestoreTransaction,
					"User points transaction restore rejected. Transaction not found.",
					StructuredLog.Combine(properties, ("Error", UserPointsError.TransactionNotFound)));

				return UserPointsResult<bool>.Fail(UserPointsError.TransactionNotFound);
			}

			_logger.LogCompleted(
				LogLevel.Information,
				DomainLogEvents.UserPoints.RestoreTransaction,
				LogLayers.ApplicationService,
				ApplicationLogOperations.UserPoints.RestoreTransaction,
				"User points transaction restored.",
				properties);

			return UserPointsResult<bool>.Ok(true);
		}
		catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
		{
			_logger.LogAborted(
				LogLevel.Information,
				DomainLogEvents.UserPoints.RestoreTransaction,
				LogLayers.ApplicationService,
				ApplicationLogOperations.UserPoints.RestoreTransaction,
				"User points transaction restore aborted.",
				properties);
			throw;
		}
		catch(Exception ex)
		{
			_logger.LogFailed(
				LogLevel.Error,
				DomainLogEvents.UserPoints.RestoreTransaction,
				LogLayers.ApplicationService,
				ApplicationLogOperations.UserPoints.RestoreTransaction,
				"User points transaction restore failed.",
				ex,
				properties);
			throw;
		}
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

		var properties = new (string Name, object? Value)[]
		{
			("ActorUserId", actorUserId),
			("UserId", userId),
			("Amount", amount),
			("CoordinatorUserId", coordinatorUserId),
			("TaskId", taskId),
		};

		using var scope = _logger.BeginExecutionScope(
			DomainLogEvents.UserPoints.CreateTransaction,
			LogLayers.ApplicationService,
			ApplicationLogOperations.UserPoints.CreateTransaction,
			properties);

		_logger.LogStarted(
			DomainLogEvents.UserPoints.CreateTransaction,
			LogLayers.ApplicationService,
			ApplicationLogOperations.UserPoints.CreateTransaction,
			"User points transaction create started.",
			properties);

		try
		{
			if(userId == Guid.Empty || amount == 0 || comment.Length == 0)
			{
				_logger.LogRejected(
					LogLevel.Warning,
					DomainLogEvents.UserPoints.CreateTransaction,
					LogLayers.ApplicationService,
					ApplicationLogOperations.UserPoints.CreateTransaction,
					"User points transaction create rejected by validation.",
					StructuredLog.Combine(properties, ("Error", UserPointsError.ValidationFailed)));
				return UserPointsResult<Guid>.Fail(UserPointsError.ValidationFailed);
			}

			if(!taskId.HasValue && !coordinatorUserId.HasValue)
			{
				_logger.LogRejected(
					LogLevel.Warning,
					DomainLogEvents.UserPoints.CreateTransaction,
					LogLayers.ApplicationService,
					ApplicationLogOperations.UserPoints.CreateTransaction,
					"User points transaction create rejected. Missing coordinatorUserId/taskId.",
					StructuredLog.Combine(properties, ("Error", UserPointsError.ValidationFailed)));
				return UserPointsResult<Guid>.Fail(UserPointsError.ValidationFailed);
			}

			if(coordinatorUserId.HasValue && coordinatorUserId.Value != actorUserId)
			{
				_logger.LogRejected(
					LogLevel.Warning,
					DomainLogEvents.UserPoints.CreateTransaction,
					LogLayers.ApplicationService,
					ApplicationLogOperations.UserPoints.CreateTransaction,
					"User points transaction create rejected. CoordinatorUserId mismatch.",
					StructuredLog.Combine(properties, ("Error", UserPointsError.ValidationFailed)));
				return UserPointsResult<Guid>.Fail(UserPointsError.ValidationFailed);
			}

			var auth = await AuthorizeCoordinatorWriteAsync(actorUserId, actorUserPassword, userId, cancellationToken);
			if(auth.Error is not null)
			{
				_logger.LogRejected(
					LogLevel.Warning,
					DomainLogEvents.UserPoints.CreateTransaction,
					LogLayers.ApplicationService,
					ApplicationLogOperations.UserPoints.CreateTransaction,
					"User points transaction create rejected.",
					StructuredLog.Combine(properties, ("Error", auth.Error.Value)));
				return UserPointsResult<Guid>.Fail(auth.Error.Value);
			}

			var currentBalance = await _transactions.GetBalanceAsync(userId, cancellationToken);
			if(currentBalance is null)
			{
				_logger.LogRejected(
					LogLevel.Warning,
					DomainLogEvents.UserPoints.CreateTransaction,
					LogLayers.ApplicationService,
					ApplicationLogOperations.UserPoints.CreateTransaction,
					"User points transaction create rejected. Target user not found.",
					StructuredLog.Combine(properties, ("Error", UserPointsError.UserNotFound)));
				return UserPointsResult<Guid>.Fail(UserPointsError.UserNotFound);
			}

			var nextBalance = currentBalance.Value + amount;
			if(amount < 0 && nextBalance < 0)
			{
				_logger.LogRejected(
					LogLevel.Warning,
					DomainLogEvents.UserPoints.CreateTransaction,
					LogLayers.ApplicationService,
					ApplicationLogOperations.UserPoints.CreateTransaction,
					"User points transaction create rejected. Negative transaction would result in negative balance.",
					StructuredLog.Combine(
						properties,
						("Error", UserPointsError.InsufficientBalance),
						("CurrentBalance", currentBalance.Value),
						("NextBalance", nextBalance)));
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

			if(id is null)
			{
				_logger.LogRejected(
					LogLevel.Warning,
					DomainLogEvents.UserPoints.CreateTransaction,
					LogLayers.ApplicationService,
					ApplicationLogOperations.UserPoints.CreateTransaction,
					"User points transaction create rejected. Target user disappeared before save.",
					StructuredLog.Combine(properties, ("Error", UserPointsError.UserNotFound)));
				return UserPointsResult<Guid>.Fail(UserPointsError.UserNotFound);
			}

			_logger.LogCompleted(
				LogLevel.Information,
				DomainLogEvents.UserPoints.CreateTransaction,
				LogLayers.ApplicationService,
				ApplicationLogOperations.UserPoints.CreateTransaction,
				"User points transaction created.",
				StructuredLog.Combine(properties, ("TransactionId", id.Value), ("NextBalance", nextBalance)));

			return UserPointsResult<Guid>.Ok(id.Value);
		}
		catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
		{
			_logger.LogAborted(
				LogLevel.Information,
				DomainLogEvents.UserPoints.CreateTransaction,
				LogLayers.ApplicationService,
				ApplicationLogOperations.UserPoints.CreateTransaction,
				"User points transaction create aborted.",
				properties);
			throw;
		}
		catch(Exception ex)
		{
			_logger.LogFailed(
				LogLevel.Error,
				DomainLogEvents.UserPoints.CreateTransaction,
				LogLayers.ApplicationService,
				ApplicationLogOperations.UserPoints.CreateTransaction,
				"User points transaction create failed.",
				ex,
				properties);
			throw;
		}
	}

	private async Task<(UserPointsError? Error, UserInternalModel? Actor)> AuthorizeSelfOrAdminReadAsync(
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

		if(actorUserId != userId
		   && !string.Equals(auth.Actor!.Role, UserRoles.Admin, StringComparison.Ordinal))
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

	private async Task<(UserPointsError? Error, UserInternalModel? Actor)> AuthorizeAdminAsync(
		Guid actorUserId,
		string actorUserPassword,
		CancellationToken cancellationToken)
	{
		var auth = await AuthenticateAsync(actorUserId, actorUserPassword, cancellationToken);
		if(auth.Error is not null)
		{
			return auth;
		}

		if(!string.Equals(auth.Actor!.Role, UserRoles.Admin, StringComparison.Ordinal))
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