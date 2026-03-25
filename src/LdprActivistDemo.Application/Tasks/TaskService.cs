using LdprActivistDemo.Application.Diagnostics;
using LdprActivistDemo.Application.Logging;
using LdprActivistDemo.Application.Tasks.Models;
using LdprActivistDemo.Application.Users;
using LdprActivistDemo.Application.Users.Models;

using Microsoft.Extensions.Logging;

namespace LdprActivistDemo.Application.Tasks;

public sealed class TaskService : ITaskService
{
	private readonly ITaskRepository _tasks;
	private readonly ITaskSubmissionRepository _submissions;
	private readonly IActorAccessService _actorAccess;
	private readonly ILogger<TaskService> _logger;

	public TaskService(ITaskRepository tasks, ITaskSubmissionRepository submissions, IActorAccessService actorAccess, ILogger<TaskService> logger)
	{
		_tasks = tasks ?? throw new ArgumentNullException(nameof(tasks));
		_submissions = submissions ?? throw new ArgumentNullException(nameof(submissions));
		_actorAccess = actorAccess ?? throw new ArgumentNullException(nameof(actorAccess));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	public async Task<TaskOperationResult> ValidateActorAsync(Guid actorUserId, string actorUserPassword, CancellationToken cancellationToken)
	{
		return await ExecuteAsync(
			DomainLogEvents.Task.ValidateActor,
			ApplicationLogOperations.Tasks.ValidateActor,
			async () =>
			{
				var authError = await TryAuthenticateActorAsync(actorUserId, actorUserPassword, cancellationToken);
				return authError is null
					? TaskOperationResult.Success()
					: TaskOperationResult.Fail(authError.Value);
			},
			cancellationToken,
			("ActorUserId", actorUserId));
	}

	public async Task<TaskOperationResult<Guid>> CreateAsync(Guid actorUserId, string actorUserPassword, TaskCreateModel model, CancellationToken cancellationToken)
	{
		return await ExecuteAsync(
			DomainLogEvents.Task.Create,
			ApplicationLogOperations.Tasks.Create,
			async () =>
			{
				var authError = await TryAuthenticateActorAsync(actorUserId, actorUserPassword, cancellationToken);
				return authError is null
					? await _tasks.CreateAsync(actorUserId, model, cancellationToken)
					: TaskOperationResult<Guid>.Fail(authError.Value);
			},
			cancellationToken,
			("ActorUserId", actorUserId),
			("RegionName", model.RegionName),
			("SettlementName", model.SettlementName));
	}

	public async Task<TaskOperationResult> UpdateAsync(Guid actorUserId, string actorUserPassword, Guid taskId, TaskUpdateModel model, CancellationToken cancellationToken)
	{
		return await ExecuteAsync(
			DomainLogEvents.Task.Update,
			ApplicationLogOperations.Tasks.Update,
			async () =>
			{
				var authError = await TryAuthenticateActorAsync(actorUserId, actorUserPassword, cancellationToken);
				return authError is null
					? await _tasks.UpdateAsync(actorUserId, taskId, model, cancellationToken)
					: TaskOperationResult.Fail(authError.Value);
			},
			cancellationToken,
			("ActorUserId", actorUserId),
			("TaskId", taskId));
	}

	public async Task<TaskOperationResult> OpenAsync(Guid actorUserId, string actorUserPassword, Guid taskId, CancellationToken cancellationToken)
	{
		return await ExecuteAsync(
			DomainLogEvents.Task.Open,
			ApplicationLogOperations.Tasks.Open,
			async () =>
			{
				var authError = await TryAuthenticateActorAsync(actorUserId, actorUserPassword, cancellationToken);
				return authError is null
					? await _tasks.OpenAsync(actorUserId, taskId, cancellationToken)
					: TaskOperationResult.Fail(authError.Value);
			},
			cancellationToken,
			("ActorUserId", actorUserId),
			("TaskId", taskId));
	}

	public async Task<TaskOperationResult> CloseAsync(Guid actorUserId, string actorUserPassword, Guid taskId, CancellationToken cancellationToken)
	{
		return await ExecuteAsync(
			DomainLogEvents.Task.Close,
			ApplicationLogOperations.Tasks.Close,
			async () =>
			{
				var authError = await TryAuthenticateActorAsync(actorUserId, actorUserPassword, cancellationToken);
				return authError is null
					? await _tasks.CloseAsync(actorUserId, taskId, cancellationToken)
					: TaskOperationResult.Fail(authError.Value);
			},
			cancellationToken,
			("ActorUserId", actorUserId), ("TaskId", taskId));
	}

	public async Task<TaskOperationResult<TaskModel>> GetCoordinatorAsync(Guid actorUserId, string actorUserPassword, Guid taskId, CancellationToken cancellationToken)
	{
		return await ExecuteAsync(
			DomainLogEvents.Task.GetCoordinator,
			ApplicationLogOperations.Tasks.GetCoordinator,
			async () =>
			{
				var authError = await TryAuthenticateActorAsync(actorUserId, actorUserPassword, cancellationToken);
				return authError is null
					? await _tasks.GetCoordinatorAsync(actorUserId, taskId, cancellationToken)
					: TaskOperationResult<TaskModel>.Fail(authError.Value);
			},
			cancellationToken,
			("ActorUserId", actorUserId), ("TaskId", taskId));
	}

	public Task<TaskOperationResult<TaskModel>> GetPublicAsync(Guid taskId, CancellationToken cancellationToken)
		=> ExecuteAsync(
			DomainLogEvents.Task.GetPublic,
			ApplicationLogOperations.Tasks.GetPublic,
			() => _tasks.GetPublicAsync(taskId, cancellationToken),
			cancellationToken,
			("TaskId", taskId));

	public Task<IReadOnlyList<TaskModel>> GetByRegionAndSettlementAsync(string regionName, string settlementName, CancellationToken cancellationToken)
		=> ExecuteReadAsync(
			DomainLogEvents.Task.GetByRegionAndSettlement,
			ApplicationLogOperations.Tasks.GetByRegionAndSettlement,
			() => _tasks.GetByRegionAndSettlementAsync(regionName, settlementName, cancellationToken),
			cancellationToken,
			("RegionName", regionName), ("SettlementName", settlementName));

	public Task<IReadOnlyList<TaskModel>> GetByRegionAsync(string regionName, CancellationToken cancellationToken)
		=> ExecuteReadAsync(
			DomainLogEvents.Task.GetByRegion,
			ApplicationLogOperations.Tasks.GetByRegion,
			() => _tasks.GetByRegionAsync(regionName, cancellationToken),
			cancellationToken,
			("RegionName", regionName));

	public Task<IReadOnlyList<TaskModel>> GetBySettlementAsync(string settlementName, CancellationToken cancellationToken)
		=> ExecuteReadAsync(
			DomainLogEvents.Task.GetBySettlement,
			ApplicationLogOperations.Tasks.GetBySettlement,
			() => _tasks.GetBySettlementAsync(settlementName, cancellationToken),
			cancellationToken,
			("SettlementName", settlementName));

	public Task<IReadOnlyList<TaskModel>> GetByCoordinatorAsync(Guid coordinatorUserId, CancellationToken cancellationToken)
		=> ExecuteReadAsync(
			DomainLogEvents.Task.GetByCoordinator,
			ApplicationLogOperations.Tasks.GetByCoordinator,
			() => _tasks.GetByCoordinatorAsync(coordinatorUserId, cancellationToken),
			cancellationToken,
			("CoordinatorUserId", coordinatorUserId));

	public Task<TaskOperationResult<IReadOnlyList<TaskModel>>> GetAvailableForUserAsync(Guid userId, CancellationToken cancellationToken)
		=> ExecuteAsync(
			DomainLogEvents.Task.GetAvailableForUser,
			ApplicationLogOperations.Tasks.GetAvailableForUser,
			() => _tasks.GetAvailableForUserAsync(userId, cancellationToken),
			cancellationToken,
			("UserId", userId));

	public async Task<TaskSubmitOperationResult> SubmitAsync(Guid actorUserId, string actorUserPassword, Guid userId, Guid taskId, TaskSubmissionCreateModel model, CancellationToken cancellationToken)
	{
		return await ExecuteSubmitAsync(
			DomainLogEvents.TaskSubmission.Submit,
			ApplicationLogOperations.Tasks.Submit,
			async () =>
			{
				if(userId == Guid.Empty || actorUserId != userId)
				{
					return TaskSubmitOperationResult.Fail(TaskOperationError.ValidationFailed);
				}

				var authError = await TryAuthenticateActorAsync(actorUserId, actorUserPassword, cancellationToken);
				return authError is null
					? await _submissions.SubmitAsync(actorUserId, userId, taskId, model, cancellationToken)
					: TaskSubmitOperationResult.Fail(authError.Value);
			},
			cancellationToken,
			("ActorUserId", actorUserId), ("UserId", userId), ("TaskId", taskId));
	}

	public async Task<TaskOperationResult> SubmitForReviewAsync(Guid actorUserId, string actorUserPassword, Guid submissionId, TaskSubmissionCreateModel model, CancellationToken cancellationToken)
	{
		return await ExecuteAsync(
			DomainLogEvents.TaskSubmission.SubmitForReview,
			ApplicationLogOperations.Tasks.SubmitForReview,
			async () =>
			{
				var authError = await TryAuthenticateActorAsync(actorUserId, actorUserPassword, cancellationToken);
				return authError is null
					? await _submissions.SubmitForReviewAsync(actorUserId, submissionId, model, cancellationToken)
					: TaskOperationResult.Fail(authError.Value);
			},
			cancellationToken,
			("ActorUserId", actorUserId), ("SubmissionId", submissionId));
	}

	public async Task<TaskOperationResult> UpdateSubmissionAsync(Guid actorUserId, string actorUserPassword, Guid submissionId, TaskSubmissionCreateModel model, CancellationToken cancellationToken)
	{
		return await ExecuteAsync(
			DomainLogEvents.TaskSubmission.Update,
			ApplicationLogOperations.Tasks.UpdateSubmission,
			async () =>
			{
				var authError = await TryAuthenticateActorAsync(actorUserId, actorUserPassword, cancellationToken);
				return authError is null
					? await _submissions.UpdateSubmissionAsync(actorUserId, submissionId, model, cancellationToken)
					: TaskOperationResult.Fail(authError.Value);
			},
			cancellationToken,
			("ActorUserId", actorUserId), ("SubmissionId", submissionId));
	}

	public async Task<TaskOperationResult> DeleteSubmissionAsync(Guid actorUserId, string actorUserPassword, Guid taskId, CancellationToken cancellationToken)
	{
		return await ExecuteAsync(
			DomainLogEvents.TaskSubmission.Delete,
			ApplicationLogOperations.Tasks.DeleteSubmission,
			async () =>
			{
				var authError = await TryAuthenticateActorAsync(actorUserId, actorUserPassword, cancellationToken);
				return authError is null
					? await _submissions.DeleteSubmissionAsync(actorUserId, taskId, cancellationToken)
					: TaskOperationResult.Fail(authError.Value);
			},
			cancellationToken,
			("ActorUserId", actorUserId), ("TaskId", taskId));
	}

	public async Task<TaskOperationResult<IReadOnlyList<UserPublicModel>>> GetSubmittedUsersAsync(Guid actorUserId, string actorUserPassword, Guid taskId, CancellationToken cancellationToken)
	{
		return await ExecuteAsync(
			DomainLogEvents.TaskSubmission.GetSubmittedUsers,
			ApplicationLogOperations.Tasks.GetSubmittedUsers,
			async () =>
			{
				var authError = await TryAuthenticateActorAsync(actorUserId, actorUserPassword, cancellationToken);
				return authError is null
					? await _submissions.GetSubmittedUsersAsync(actorUserId, taskId, cancellationToken)
					: TaskOperationResult<IReadOnlyList<UserPublicModel>>.Fail(authError.Value);
			},
			cancellationToken,
			("ActorUserId", actorUserId), ("TaskId", taskId));
	}

	public async Task<TaskOperationResult<IReadOnlyList<UserPublicModel>>> GetApprovedUsersAsync(Guid actorUserId, string actorUserPassword, Guid taskId, CancellationToken cancellationToken)
	{
		return await ExecuteAsync(
			DomainLogEvents.TaskSubmission.GetApprovedUsers,
			ApplicationLogOperations.Tasks.GetApprovedUsers,
			async () =>
			{
				var authError = await TryAuthenticateActorAsync(actorUserId, actorUserPassword, cancellationToken);
				return authError is null
					? await _submissions.GetApprovedUsersAsync(actorUserId, taskId, cancellationToken)
					: TaskOperationResult<IReadOnlyList<UserPublicModel>>.Fail(authError.Value);
			},
			cancellationToken,
			("ActorUserId", actorUserId), ("TaskId", taskId));
	}

	public async Task<TaskOperationResult<SubmissionUserViewModel>> GetSubmittedUserAsync(Guid actorUserId, string actorPassword, Guid taskId, Guid userId, CancellationToken cancellationToken)
	{
		return await ExecuteAsync(
			DomainLogEvents.TaskSubmission.GetSubmittedUser,
			ApplicationLogOperations.Tasks.GetSubmittedUser,
			async () =>
			{
				var authError = await TryAuthenticateActorAsync(actorUserId, actorPassword, cancellationToken);
				return authError is null
					? await _submissions.GetSubmittedUserAsync(actorUserId, taskId, userId, cancellationToken)
					: TaskOperationResult<SubmissionUserViewModel>.Fail(authError.Value);
			},
			cancellationToken,
			("ActorUserId", actorUserId), ("TaskId", taskId), ("UserId", userId));
	}

	public async Task<TaskOperationResult> ApproveAsync(Guid actorUserId, string actorPassword, Guid submissionId, CancellationToken cancellationToken)
	{
		return await ExecuteAsync(
			DomainLogEvents.TaskSubmission.Approve,
			ApplicationLogOperations.Tasks.ApproveSubmission,
			async () =>
			{
				var authError = await TryAuthenticateActorAsync(actorUserId, actorPassword, cancellationToken);
				return authError is null
					? await _submissions.ApproveAsync(actorUserId, submissionId, DateTimeOffset.UtcNow, cancellationToken)
					: TaskOperationResult.Fail(authError.Value);
			},
			cancellationToken,
			("ActorUserId", actorUserId), ("SubmissionId", submissionId));
	}

	public async Task<TaskOperationResult> RejectAsync(Guid actorUserId, string actorPassword, Guid submissionId, CancellationToken cancellationToken)
	{
		return await ExecuteAsync(
			DomainLogEvents.TaskSubmission.Reject,
			ApplicationLogOperations.Tasks.RejectSubmission,
			async () =>
			{
				var authError = await TryAuthenticateActorAsync(actorUserId, actorPassword, cancellationToken);
				return authError is null
					? await _submissions.RejectAsync(actorUserId, submissionId, DateTimeOffset.UtcNow, cancellationToken)
					: TaskOperationResult.Fail(authError.Value);
			},
			cancellationToken,
			("ActorUserId", actorUserId), ("SubmissionId", submissionId));
	}

	public async Task<TaskOperationResult<IReadOnlyList<TaskModel>>> GetByUserSubmittedAsync(Guid actorUserId, string actorUserPassword, CancellationToken cancellationToken)
	{
		return await ExecuteAsync(
			DomainLogEvents.Task.GetByUserSubmitted,
			ApplicationLogOperations.Tasks.GetByUserSubmitted,
			async () =>
			{
				var authError = await TryAuthenticateActorAsync(actorUserId, actorUserPassword, cancellationToken);
				return authError is null
					? await _tasks.GetByUserSubmittedAsync(actorUserId, cancellationToken)
					: TaskOperationResult<IReadOnlyList<TaskModel>>.Fail(authError.Value);
			},
			cancellationToken,
			("ActorUserId", actorUserId));
	}

	public async Task<TaskOperationResult<IReadOnlyList<TaskModel>>> GetByUserApprovedAsync(Guid actorUserId, string actorUserPassword, CancellationToken cancellationToken)
	{
		return await ExecuteAsync(
			DomainLogEvents.Task.GetByUserApproved,
			ApplicationLogOperations.Tasks.GetByUserApproved,
			async () =>
			{
				var authError = await TryAuthenticateActorAsync(actorUserId, actorUserPassword, cancellationToken);
				return authError is null
					? await _tasks.GetByUserApprovedAsync(actorUserId, cancellationToken)
					: TaskOperationResult<IReadOnlyList<TaskModel>>.Fail(authError.Value);
			},
			cancellationToken,
			("ActorUserId", actorUserId));
	}

	public async Task<TaskOperationResult<IReadOnlyList<TaskSubmissionModel>>> GetSubmissionReviewerFeedAsync(
		Guid actorUserId,
		string actorUserPassword,
		Guid reviewerUserId,
		Guid? taskId,
		Guid? userId,
		string? decisionStatus,
		CancellationToken cancellationToken)
	{
		return await ExecuteAsync(
			DomainLogEvents.TaskSubmission.GetReviewerFeed,
			ApplicationLogOperations.Tasks.GetSubmissionReviewerFeed,
			async () =>
			{
				if(reviewerUserId == Guid.Empty)
				{
					return TaskOperationResult<IReadOnlyList<TaskSubmissionModel>>.Fail(TaskOperationError.ValidationFailed);
				}

				var authError = await TryAuthenticateActorAsync(actorUserId, actorUserPassword, cancellationToken);
				return authError is null
					? await _submissions.GetReviewerFeedAsync(reviewerUserId, taskId, userId, decisionStatus, cancellationToken)
					: TaskOperationResult<IReadOnlyList<TaskSubmissionModel>>.Fail(authError.Value);
			},
			cancellationToken,
			("ActorUserId", actorUserId),
			("ReviewerUserId", reviewerUserId),
			("TaskId", taskId), ("UserId", userId), ("DecisionStatus", decisionStatus));
	}

	public async Task<TaskOperationResult<IReadOnlyList<TaskSubmissionModel>>> GetSubmissionExecutorFeedAsync(
		Guid actorUserId,
		string actorUserPassword,
		Guid? taskId,
		Guid userId,
		string? decisionStatus,
		CancellationToken cancellationToken)
	{
		return await ExecuteAsync(
			DomainLogEvents.TaskSubmission.GetExecutorFeed,
			ApplicationLogOperations.Tasks.GetSubmissionExecutorFeed,
			async () =>
			{
				if(userId == Guid.Empty || actorUserId != userId)
				{
					return TaskOperationResult<IReadOnlyList<TaskSubmissionModel>>.Fail(TaskOperationError.ValidationFailed);
				}

				if(taskId.HasValue && taskId.Value == Guid.Empty)
				{
					return TaskOperationResult<IReadOnlyList<TaskSubmissionModel>>.Fail(TaskOperationError.ValidationFailed);
				}

				var authError = await TryAuthenticateActorAsync(actorUserId, actorUserPassword, cancellationToken);
				return authError is null
					? await _submissions.GetExecutorFeedAsync(taskId, userId, decisionStatus, cancellationToken)
					: TaskOperationResult<IReadOnlyList<TaskSubmissionModel>>.Fail(authError.Value);
			},
			cancellationToken,
			("ActorUserId", actorUserId), ("TaskId", taskId), ("UserId", userId), ("DecisionStatus", decisionStatus));
	}

	public async Task<TaskOperationResult<IReadOnlyList<Guid>>> GetTaskIdsByUserSubmissionStatusAsync(
		Guid actorUserId,
		string actorUserPassword,
		Guid userId,
		string decisionStatus,
		CancellationToken cancellationToken)
	{
		return await ExecuteAsync(
			DomainLogEvents.TaskSubmission.GetTaskIdsByUserDecisionStatus,
			ApplicationLogOperations.Tasks.GetTaskIdsByUserSubmissionStatus,
			async () =>
			{
				if(userId == Guid.Empty || string.IsNullOrWhiteSpace(decisionStatus))
				{
					return TaskOperationResult<IReadOnlyList<Guid>>.Fail(TaskOperationError.ValidationFailed);
				}

				var authError = await TryAuthenticateActorAsync(actorUserId, actorUserPassword, cancellationToken);
				return authError is null
					? await _submissions.GetTaskIdsByUserAndDecisionStatusAsync(
						userId,
						decisionStatus,
						cancellationToken)
					: TaskOperationResult<IReadOnlyList<Guid>>.Fail(authError.Value);
			},
			cancellationToken,
			("ActorUserId", actorUserId),
			("UserId", userId),
			("DecisionStatus", decisionStatus));
	}

	public async Task<TaskOperationResult<IReadOnlyList<Guid>>> GetTaskIdsWithAnySubmissionByUserAsync(
		Guid actorUserId,
		string actorUserPassword,
		Guid userId,
		CancellationToken cancellationToken)
	{
		return await ExecuteAsync(
			DomainLogEvents.TaskSubmission.GetTaskIdsWithAnySubmissionByUser,
			ApplicationLogOperations.Tasks.GetTaskIdsWithAnySubmissionByUser,
			async () =>
			{
				if(userId == Guid.Empty)
				{
					return TaskOperationResult<IReadOnlyList<Guid>>.Fail(TaskOperationError.ValidationFailed);
				}

				var authError = await TryAuthenticateActorAsync(actorUserId, actorUserPassword, cancellationToken);
				return authError is null
					? await _submissions.GetTaskIdsWithAnySubmissionByUserAsync(
						userId,
						cancellationToken)
					: TaskOperationResult<IReadOnlyList<Guid>>.Fail(authError.Value);
			},
			cancellationToken,
			("ActorUserId", actorUserId),
			("UserId", userId));
	}

	public async Task<TaskOperationResult<TaskSubmissionModel>> GetSubmissionByIdAsync(Guid actorUserId, string actorUserPassword, Guid submissionId, CancellationToken cancellationToken)
	{
		return await ExecuteAsync(
			DomainLogEvents.TaskSubmission.GetById,
			ApplicationLogOperations.Tasks.GetSubmissionById,
			async () =>
			{
				var authError = await TryAuthenticateActorAsync(actorUserId, actorUserPassword, cancellationToken);
				return authError is null
					? await _submissions.GetByIdAsync(actorUserId, submissionId, cancellationToken)
					: TaskOperationResult<TaskSubmissionModel>.Fail(authError.Value);
			},
			cancellationToken,
			("ActorUserId", actorUserId), ("SubmissionId", submissionId));
	}

	public async Task<TaskOperationResult<IReadOnlyList<UserPublicModel>>> GetTaskUsersAsync(
		Guid actorUserId,
		string actorUserPassword,
		Guid taskId,
		string? taskStatus,
		CancellationToken cancellationToken)
	{
		return await ExecuteAsync(
			DomainLogEvents.TaskSubmission.GetTaskUsers,
			ApplicationLogOperations.Tasks.GetTaskUsers,
			async () =>
			{
				if(taskId == Guid.Empty)
				{
					return TaskOperationResult<IReadOnlyList<UserPublicModel>>.Fail(TaskOperationError.ValidationFailed);
				}

				var authError = await TryAuthenticateActorAsync(actorUserId, actorUserPassword, cancellationToken);
				return authError is null
					? await _submissions.GetTaskUsersAsync(actorUserId, taskId, taskStatus, cancellationToken)
					: TaskOperationResult<IReadOnlyList<UserPublicModel>>.Fail(authError.Value);
			},
			cancellationToken,
			("ActorUserId", actorUserId), ("TaskId", taskId), ("TaskStatus", taskStatus));
	}

	private async Task<TaskOperationError?> TryAuthenticateActorAsync(
		Guid actorUserId,
		string actorUserPassword,
		CancellationToken cancellationToken)
	{
		var auth = await _actorAccess.AuthenticateAsync(actorUserId, actorUserPassword, cancellationToken);
		if(auth.IsSuccess)
		{
			return null;
		}

		return auth.Error switch
		{
			ActorAuthenticationError.ValidationFailed => TaskOperationError.ValidationFailed,
			_ => TaskOperationError.InvalidCredentials,
		};
	}

	private async Task<TaskOperationResult> ExecuteAsync(
		string eventName,
		string operationName,
		Func<Task<TaskOperationResult>> action,
		CancellationToken cancellationToken,
		params (string Name, object? Value)[] properties)
	{
		using var scope = _logger.BeginExecutionScope(eventName, LogLayers.ApplicationService, operationName, properties);

		_logger.LogStarted(
			eventName,
			LogLayers.ApplicationService,
			operationName,
			"Task application service operation started.",
			properties);

		try
		{
			var result = await action();
			var resultProperties = StructuredLog.Combine(
				properties,
				("Error", result.Error));

			if(result.IsSuccess)
			{
				_logger.LogCompleted(
					LogLevel.Information,
					eventName,
					LogLayers.ApplicationService,
					operationName,
					"Task application service operation completed.",
					resultProperties);

				return result;
			}

			_logger.LogRejected(
				LogLevel.Warning,
				eventName,
				LogLayers.ApplicationService,
				operationName,
				"Task application service operation rejected.",
				resultProperties);

			return result;
		}
		catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
		{
			_logger.LogAborted(
				LogLevel.Information,
				eventName,
				LogLayers.ApplicationService,
				operationName,
				"Task application service operation aborted.",
				properties);
			throw;
		}
		catch(Exception ex)
		{
			_logger.LogFailed(
				LogLevel.Error,
				eventName,
				LogLayers.ApplicationService,
				operationName,
				"Task application service operation failed.",
				ex,
				properties);
			throw;
		}
	}

	private async Task<TaskOperationResult<T>> ExecuteAsync<T>(
		string eventName,
		string operationName,
		Func<Task<TaskOperationResult<T>>> action,
		CancellationToken cancellationToken,
		params (string Name, object? Value)[] properties)
	{
		using var scope = _logger.BeginExecutionScope(eventName, LogLayers.ApplicationService, operationName, properties);

		_logger.LogStarted(
			eventName,
			LogLayers.ApplicationService,
			operationName,
			"Task application service operation started.",
			properties);

		try
		{
			var result = await action();
			var resultProperties = new List<(string Name, object? Value)>(properties.Length + 3);
			resultProperties.AddRange(properties);
			resultProperties.Add(("Error", result.Error));
			resultProperties.Add(("HasValue", result.Value is not null));

			if(result.Value is System.Collections.ICollection collection)
			{
				resultProperties.Add(("Count", collection.Count));
			}

			if(result.IsSuccess)
			{
				_logger.LogCompleted(
					LogLevel.Information,
					eventName,
					LogLayers.ApplicationService,
					operationName,
					"Task application service operation completed.",
					resultProperties.ToArray());

				return result;
			}

			_logger.LogRejected(
				LogLevel.Warning,
				eventName,
				LogLayers.ApplicationService,
				operationName,
				"Task application service operation rejected.",
				resultProperties.ToArray());

			return result;
		}
		catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
		{
			_logger.LogAborted(
				LogLevel.Information,
				eventName,
				LogLayers.ApplicationService,
				operationName,
				"Task application service operation aborted.",
				properties);
			throw;
		}
		catch(Exception ex)
		{
			_logger.LogFailed(
				LogLevel.Error,
				eventName,
				LogLayers.ApplicationService,
				operationName,
				"Task application service operation failed.",
				ex,
				properties);
			throw;
		}
	}

	private async Task<TaskSubmitOperationResult> ExecuteSubmitAsync(
		string eventName,
		string operationName,
		Func<Task<TaskSubmitOperationResult>> action,
		CancellationToken cancellationToken,
		params (string Name, object? Value)[] properties)
	{
		using var scope = _logger.BeginExecutionScope(eventName, LogLayers.ApplicationService, operationName, properties);

		_logger.LogStarted(
			eventName,
			LogLayers.ApplicationService,
			operationName,
			"Task submission application service operation started.",
			properties);

		try
		{
			var result = await action();
			var resultProperties = StructuredLog.Combine(
				properties,
				("Error", result.Error),
				("IsCreated", result.IsCreated));

			if(result.IsSuccess)
			{
				_logger.LogCompleted(
					LogLevel.Information,
					eventName,
					LogLayers.ApplicationService,
					operationName,
					"Task submission application service operation completed.",
					resultProperties);

				return result;
			}

			_logger.LogRejected(
				LogLevel.Warning,
				eventName,
				LogLayers.ApplicationService,
				operationName,
				"Task submission application service operation rejected.",
				resultProperties);

			return result;
		}
		catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
		{
			_logger.LogAborted(
				LogLevel.Information,
				eventName,
				LogLayers.ApplicationService,
				operationName,
				"Task submission application service operation aborted.",
				properties);
			throw;
		}
		catch(Exception ex)
		{
			_logger.LogFailed(
				LogLevel.Error,
				eventName,
				LogLayers.ApplicationService,
				operationName,
				"Task submission application service operation failed.",
				ex,
				properties);
			throw;
		}
	}

	private async Task<IReadOnlyList<TaskModel>> ExecuteReadAsync(
		string eventName,
		string operationName,
		Func<Task<IReadOnlyList<TaskModel>>> action,
		CancellationToken cancellationToken,
		params (string Name, object? Value)[] properties)
	{
		using var scope = _logger.BeginExecutionScope(eventName, LogLayers.ApplicationService, operationName, properties);

		_logger.LogStarted(
			eventName,
			LogLayers.ApplicationService,
			operationName,
			"Task read application service operation started.",
			properties);

		try
		{
			var result = await action();
			_logger.LogCompleted(
				LogLevel.Debug,
				eventName,
				LogLayers.ApplicationService,
				operationName,
				"Task read application service operation completed.",
				StructuredLog.Combine(
					properties,
					("Count", result.Count)));

			return result;
		}
		catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
		{
			_logger.LogAborted(
				LogLevel.Information,
				eventName,
				LogLayers.ApplicationService,
				operationName,
				"Task read application service operation aborted.",
				properties);
			throw;
		}
		catch(Exception ex)
		{
			_logger.LogFailed(
				LogLevel.Error,
				eventName,
				LogLayers.ApplicationService,
				operationName,
				"Task read application service operation failed.",
				ex,
				properties);
			throw;
		}
	}
}