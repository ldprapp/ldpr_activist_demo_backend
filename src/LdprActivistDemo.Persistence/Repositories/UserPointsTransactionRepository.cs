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
			.Where(x => x.UserId == userId)
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
				x.TaskId))
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
			CoordinatorUserId = coordinatorUserId,
			TaskId = taskId,
		});

		await _db.SaveChangesAsync(cancellationToken);
		return id;
	}
}