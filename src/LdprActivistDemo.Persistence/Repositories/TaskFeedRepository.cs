using LdprActivistDemo.Application.Diagnostics;
using LdprActivistDemo.Application.Tasks;
using LdprActivistDemo.Persistence.Logging;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LdprActivistDemo.Persistence;

public sealed class TaskFeedRepository : ITaskFeedRepository
{
	private readonly AppDbContext _db;
	private readonly ILogger<TaskFeedRepository> _logger;

	public TaskFeedRepository(AppDbContext db, ILogger<TaskFeedRepository> logger)
	{
		_db = db ?? throw new ArgumentNullException(nameof(db));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	public async Task<IReadOnlyList<Guid>> GetAllTaskIdsAsync(CancellationToken cancellationToken)
	{
		using var scope = _logger.BeginExecutionScope(
			DomainLogEvents.Task.Repository.GetAllIds,
			LogLayers.PersistenceRepository,
			PersistenceLogOperations.Tasks.GetAllIds);

		_logger.LogStarted(
			DomainLogEvents.Task.Repository.GetAllIds,
			LogLayers.PersistenceRepository,
			PersistenceLogOperations.Tasks.GetAllIds,
			"Task ids feed load started.");

		try
		{
			var ids = await _db.Tasks
				.AsNoTracking()
				.OrderByDescending(x => x.PublishedAt)
				.ThenByDescending(x => x.Id)
				.Select(x => x.Id)
				.ToListAsync(cancellationToken);

			_logger.LogCompleted(
				LogLevel.Debug,
				DomainLogEvents.Task.Repository.GetAllIds,
				LogLayers.PersistenceRepository,
				PersistenceLogOperations.Tasks.GetAllIds,
				"Task ids feed load completed.",
				("Count", ids.Count));

			return ids;
		}
		catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
		{
			_logger.LogAborted(
				LogLevel.Information,
				DomainLogEvents.Task.Repository.GetAllIds,
				LogLayers.PersistenceRepository,
				PersistenceLogOperations.Tasks.GetAllIds,
				"Task ids feed load aborted.");
			throw;
		}
		catch(Exception ex)
		{
			_logger.LogFailed(
				LogLevel.Error,
				DomainLogEvents.Task.Repository.GetAllIds,
				LogLayers.PersistenceRepository,
				PersistenceLogOperations.Tasks.GetAllIds,
				"Task ids feed load failed.",
				ex);
			throw;
		}
	}
}