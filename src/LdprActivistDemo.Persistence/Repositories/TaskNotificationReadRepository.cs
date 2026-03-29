using LdprActivistDemo.Application.Diagnostics;
using LdprActivistDemo.Application.Logging;
using LdprActivistDemo.Application.Push;
using LdprActivistDemo.Persistence.Logging;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LdprActivistDemo.Persistence;

public sealed class TaskNotificationReadRepository : ITaskNotificationReadRepository
{
	private const string EventGetTaskCreatedContext = "push.context.repository.task_created";
	private const string EventGetSubmissionDecisionContext = "push.context.repository.submission_decision";

	private readonly AppDbContext _db;
	private readonly ILogger<TaskNotificationReadRepository> _logger;

	public TaskNotificationReadRepository(
		AppDbContext db,
		ILogger<TaskNotificationReadRepository> logger)
	{
		_db = db ?? throw new ArgumentNullException(nameof(db));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	public async Task<TaskCreatedNotificationContext?> GetTaskCreatedContextAsync(
		Guid taskId,
		CancellationToken cancellationToken)
	{
		return await ExecuteReadSingleAsync(
			EventGetTaskCreatedContext,
			PersistenceLogOperations.PushNotificationContext.GetTaskCreated,
			async () => await _db.Tasks
				.AsNoTracking()
				.Where(x => x.Id == taskId)
				.Select(x => new TaskCreatedNotificationContext(
					x.Id,
					x.Title,
					x.RegionId,
					x.SettlementId))
				.FirstOrDefaultAsync(cancellationToken),
			cancellationToken,
			("TaskId", taskId));
	}

	public async Task<SubmissionDecisionNotificationContext?> GetSubmissionDecisionContextAsync(
		Guid submissionId,
		CancellationToken cancellationToken)
	{
		return await ExecuteReadSingleAsync(
			EventGetSubmissionDecisionContext,
			PersistenceLogOperations.PushNotificationContext.GetSubmissionDecision,
			async () => await _db.TaskSubmissions
				.AsNoTracking()
				.Where(x => x.Id == submissionId)
				.Select(x => new SubmissionDecisionNotificationContext(
					x.Id,
					x.TaskId,
					x.UserId,
					x.Task.Title,
					x.DecisionStatus))
				.FirstOrDefaultAsync(cancellationToken),
			cancellationToken,
			("SubmissionId", submissionId));
	}

	private async Task<T?> ExecuteReadSingleAsync<T>(
		string eventName,
		string operationName,
		Func<Task<T?>> action,
		CancellationToken cancellationToken,
		params (string Name, object? Value)[] properties)
		where T : class
	{
		using var scope = _logger.BeginExecutionScope(
			eventName,
			LogLayers.PersistenceRepository,
			operationName,
			properties);

		_logger.LogStarted(
			eventName,
			LogLayers.PersistenceRepository,
			operationName,
			"Task notification context read started.",
			properties);

		try
		{
			var result = await action();

			_logger.LogCompleted(
				LogLevel.Debug,
				eventName,
				LogLayers.PersistenceRepository,
				operationName,
				"Task notification context read completed.",
				StructuredLog.Combine(properties, ("Found", result is not null)));

			return result;
		}
		catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
		{
			_logger.LogAborted(
				LogLevel.Information,
				eventName,
				LogLayers.PersistenceRepository,
				operationName,
				"Task notification context read aborted.",
				properties);
			throw;
		}
		catch(Exception ex)
		{
			_logger.LogFailed(
				LogLevel.Error,
				eventName,
				LogLayers.PersistenceRepository,
				operationName,
				"Task notification context read failed.",
				ex,
				properties);
			throw;
		}
	}
}