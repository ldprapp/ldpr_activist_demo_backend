using LdprActivistDemo.Application.Tasks;

using Microsoft.EntityFrameworkCore;

namespace LdprActivistDemo.Persistence;

public sealed class TaskFeedRepository : ITaskFeedRepository
{
	private readonly AppDbContext _db;

	public TaskFeedRepository(AppDbContext db)
	{
		_db = db;
	}

	public async Task<IReadOnlyList<Guid>> GetAllTaskIdsAsync(CancellationToken cancellationToken)
		=> await _db.Tasks
			.AsNoTracking()
			.OrderByDescending(x => x.PublishedAt)
			.Select(x => x.Id)
			.ToListAsync(cancellationToken);
}