using LdprActivistDemo.Application.UserPoints;
using LdprActivistDemo.Application.UserPoints.Models;

using Microsoft.EntityFrameworkCore;

namespace LdprActivistDemo.Persistence;

public sealed class UserPointsTransactionRepository : IUserPointsTransactionRepository
{
	private const string InitializationComment = "User initialization transaction.";

	private readonly AppDbContext _db;

	public UserPointsTransactionRepository(AppDbContext db)
	{
		_db = db ?? throw new ArgumentNullException(nameof(db));
	}

	public async Task<int?> GetBalanceAsync(Guid userId, CancellationToken cancellationToken)
	{
		if(userId == Guid.Empty)
		{
			return null;
		}

		var userExists = await _db.Users.AsNoTracking()
			.AnyAsync(x => x.Id == userId, cancellationToken);

		if(!userExists)
		{
			return null;
		}

		var sum = await _db.UserPointsTransactions.AsNoTracking()
			.Where(x => x.UserId == userId && !x.IsCancelled)
			.SumAsync(x => (int?)x.Amount, cancellationToken);

		return sum ?? 0;
	}

	public async Task<IReadOnlyList<UserPointsTransactionModel>?> GetTransactionsAsync(
		Guid userId,
		bool excludeInitialization,
		CancellationToken cancellationToken)
	{
		if(userId == Guid.Empty)
		{
			return null;
		}

		var userExists = await _db.Users.AsNoTracking()
			.AnyAsync(x => x.Id == userId, cancellationToken);

		if(!userExists)
		{
			return null;
		}

		IQueryable<UserPointsTransaction> query = _db.UserPointsTransactions.AsNoTracking()
			.Where(x => x.UserId == userId);

		if(excludeInitialization)
		{
			query = query.Where(x => !(x.Amount == 0 && x.Comment == InitializationComment));
		}

		var list = await query
			.OrderByDescending(x => x.TransactionAt)
			.ThenByDescending(x => x.Id)
			.Select(x => new UserPointsTransactionModel(
				x.Id,
				x.UserId,
				x.Amount,
				x.TransactionAt,
				x.Comment,
				x.CoordinatorUserId,
				x.TaskId,
				x.IsCancelled,
				x.CancellationComment,
				x.CancelledAtUtc,
				x.CancelledByAdminUserId))
			.ToListAsync(cancellationToken);

		return list;
	}

	public async Task<Guid?> CreateAsync(
		Guid userId,
		int amount,
		string comment,
		DateTimeOffset transactionAtUtc,
		Guid? coordinatorUserId,
		Guid? taskId,
		CancellationToken cancellationToken)
	{
		coordinatorUserId = coordinatorUserId.HasValue && coordinatorUserId.Value == Guid.Empty
			? null
			: coordinatorUserId;
		taskId = taskId.HasValue && taskId.Value == Guid.Empty
			? null
			: taskId;

		if(userId == Guid.Empty)
		{
			return null;
		}

		var userExists = await _db.Users.AsNoTracking()
			.AnyAsync(x => x.Id == userId, cancellationToken);

		if(!userExists)
		{
			return null;
		}

		var id = Guid.NewGuid();

		_db.UserPointsTransactions.Add(new UserPointsTransaction
		{
			Id = id,
			UserId = userId,
			Amount = amount,
			TransactionAt = transactionAtUtc,
			Comment = comment.Trim(),
			IsCancelled = false,
			CancellationComment = string.Empty,
			CancelledAtUtc = null,
			CancelledByAdminUserId = null,
			CoordinatorUserId = coordinatorUserId,
			TaskId = taskId,
		});

		await _db.SaveChangesAsync(cancellationToken);
		return id;
	}

	public async Task<bool> CancelAsync(
		Guid transactionId,
		string cancellationComment,
		Guid cancelledByAdminUserId,
		DateTimeOffset cancelledAtUtc,
		CancellationToken cancellationToken)
	{
		if(transactionId == Guid.Empty || cancelledByAdminUserId == Guid.Empty)
		{
			return false;
		}

		var transaction = await _db.UserPointsTransactions
			.FirstOrDefaultAsync(
				x => x.Id == transactionId,
				cancellationToken);

		if(transaction is null)
		{
			return false;
		}

		transaction.IsCancelled = true;
		transaction.CancellationComment = (cancellationComment ?? string.Empty).Trim();
		transaction.CancelledAtUtc = cancelledAtUtc;
		transaction.CancelledByAdminUserId = cancelledByAdminUserId;

		await _db.SaveChangesAsync(cancellationToken);
		return true;
	}

	public async Task<bool> RestoreAsync(
		Guid transactionId,
		CancellationToken cancellationToken)
	{
		if(transactionId == Guid.Empty)
		{
			return false;
		}

		var transaction = await _db.UserPointsTransactions
			.FirstOrDefaultAsync(
				x => x.Id == transactionId,
				cancellationToken);

		if(transaction is null)
		{
			return false;
		}

		transaction.IsCancelled = false;
		transaction.CancellationComment = string.Empty;
		transaction.CancelledAtUtc = null;
		transaction.CancelledByAdminUserId = null;

		await _db.SaveChangesAsync(cancellationToken);
		return true;
	}
}