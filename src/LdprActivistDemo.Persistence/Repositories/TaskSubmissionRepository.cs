using LdprActivistDemo.Application.Diagnostics;
using LdprActivistDemo.Application.Logging;
using LdprActivistDemo.Application.Tasks;
using LdprActivistDemo.Application.Tasks.Models;
using LdprActivistDemo.Application.Users;
using LdprActivistDemo.Application.Users.Models;
using LdprActivistDemo.Contracts.Tasks;
using LdprActivistDemo.Persistence.Logging;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using TaskStatus = LdprActivistDemo.Contracts.Tasks.TaskStatus;

namespace LdprActivistDemo.Persistence;

public sealed class TaskSubmissionRepository : ITaskSubmissionRepository
{
	private readonly AppDbContext _db;
	private readonly IUserRepository _users;
	private readonly ILogger<TaskSubmissionRepository> _logger;

	public TaskSubmissionRepository(AppDbContext db, IUserRepository users, ILogger<TaskSubmissionRepository> logger)
	{
		_db = db ?? throw new ArgumentNullException(nameof(db));
		_users = users ?? throw new ArgumentNullException(nameof(users));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	public async Task<TaskSubmitOperationResult> SubmitAsync(Guid actorUserId, Guid userId, Guid taskId, TaskSubmissionCreateModel model, CancellationToken cancellationToken)
	{
		return await ExecuteSubmitAsync(
			DomainLogEvents.TaskSubmission.Repository.Submit,
			PersistenceLogOperations.TaskSubmissions.Submit,
			async () =>
			{
				var actor = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == actorUserId, cancellationToken);
				if(actor is null)
				{
					return TaskSubmitOperationResult.Fail(TaskOperationError.InvalidCredentials);
				}

				var user = actorUserId == userId
					? actor
					: await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
				if(user is null)
				{
					return TaskSubmitOperationResult.Fail(TaskOperationError.UserNotFound);
				}

				var task = await _db.Tasks.AsNoTracking().FirstOrDefaultAsync(x => x.Id == taskId, cancellationToken);
				if(task is null)
				{
					return TaskSubmitOperationResult.Fail(TaskOperationError.TaskNotFound);
				}

				var isReusable = string.Equals(task.ReuseType, TaskReuseType.Reusable, StringComparison.Ordinal);

				if(string.Equals(task.Status, TaskStatus.Closed, StringComparison.Ordinal))
				{
					return TaskSubmitOperationResult.Fail(TaskOperationError.TaskClosed);
				}

				var geoOk = task.RegionId == user.RegionId
					&& (task.SettlementId is null || user.SettlementId == task.SettlementId.Value);
				if(!geoOk)
				{
					return TaskSubmitOperationResult.Fail(TaskOperationError.TaskAccessDenied);
				}

				if(UserRoleRules.HasCoordinatorAccess(actor.Role))
				{
					var isAuthor = task.AuthorUserId == actorUserId;
					var isTrustedCoordinator = await _db.TaskTrustedCoordinators.AsNoTracking()
						.AnyAsync(x => x.TaskId == taskId && x.CoordinatorUserId == actorUserId, cancellationToken);
					if(isAuthor || isTrustedCoordinator)
					{
						return TaskSubmitOperationResult.Fail(TaskOperationError.TaskAccessDenied);
					}
				}

				var isAutoVerification = string.Equals(task.VerificationType, TaskVerificationType.Auto, StringComparison.Ordinal);
				var isImmediateAutoApproval =
					isAutoVerification && string.Equals(task.AutoVerificationActionType, TaskAutoVerificationActionType.Auto, StringComparison.Ordinal);

				if(!isReusable)
				{
					var existing = await _db.TaskSubmissions
						.AsNoTracking()
						.FirstOrDefaultAsync(x => x.TaskId == taskId && x.UserId == userId, cancellationToken);

					if(existing is not null)
					{
						return TaskSubmitOperationResult.Fail(existing.DecisionStatus == TaskSubmissionDecisionStatus.Approve
							? TaskOperationError.AlreadySubmitted
							: TaskOperationError.SubmissionAlreadyExists);
					}
				}

				var decidedAt = isImmediateAutoApproval ? model.SubmittedAt : (DateTimeOffset?)null;

				var submission = new TaskSubmission
				{
					Id = Guid.NewGuid(),
					TaskId = taskId,
					UserId = userId,
					SubmittedAt = model.SubmittedAt,
					DecisionStatus = isImmediateAutoApproval
						? TaskSubmissionDecisionStatus.Approve
						: TaskSubmissionDecisionStatus.InProgress,
					DecidedByCoordinatorId = null,
					DecidedAt = decidedAt,
					ProofText = null,
				};

				_db.TaskSubmissions.Add(submission);

				if(isImmediateAutoApproval && task.RewardPoints != 0)
				{
					_db.UserPointsTransactions.Add(new UserPointsTransaction
					{
						Id = Guid.NewGuid(),
						UserId = userId,
						Amount = task.RewardPoints,
						TransactionAt = decidedAt!.Value,
						TaskId = taskId,
						Comment = $"Points for approval of submission {submission.Id:D}.",
					});
				}

				await _db.SaveChangesAsync(cancellationToken);
				return TaskSubmitOperationResult.Created();
			},
			cancellationToken,
			("ActorUserId", actorUserId),
			("UserId", userId),
			("TaskId", taskId));
	}

	public async Task<TaskOperationResult> SubmitForReviewAsync(Guid actorUserId, Guid submissionId, TaskSubmissionCreateModel model, CancellationToken cancellationToken)
	{
		return await ExecuteOperationAsync(
			DomainLogEvents.TaskSubmission.Repository.SubmitForReview,
			PersistenceLogOperations.TaskSubmissions.SubmitForReview,
			async () =>
			{
				var submission = await _db.TaskSubmissions
					.Include(x => x.PhotoImages)
					.FirstOrDefaultAsync(x => x.Id == submissionId && x.UserId == actorUserId, cancellationToken);
				if(submission is null)
				{
					return TaskOperationResult.Fail(TaskOperationError.SubmissionNotFound);
				}

				var task = await _db.Tasks.AsNoTracking().FirstOrDefaultAsync(x => x.Id == submission.TaskId, cancellationToken);
				if(task is null)
				{
					return TaskOperationResult.Fail(TaskOperationError.TaskNotFound);
				}

				if(string.Equals(task.VerificationType, TaskVerificationType.Auto, StringComparison.Ordinal))
				{
					return TaskOperationResult.Fail(TaskOperationError.TaskAutoVerificationNotSupported);
				}

				if(string.Equals(task.Status, TaskStatus.Closed, StringComparison.Ordinal))
				{
					return TaskOperationResult.Fail(TaskOperationError.TaskClosed);
				}

				if(!string.Equals(submission.DecisionStatus, TaskSubmissionDecisionStatus.InProgress, StringComparison.Ordinal))
				{
					return TaskOperationResult.Fail(TaskOperationError.ValidationFailed);
				}

				var newPhotoIds = (model.PhotoImageIds ?? Array.Empty<Guid>())
					.Where(x => x != Guid.Empty)
					.Distinct()
					.ToArray();

				if(newPhotoIds.Length > 0)
				{
					var count = await _db.Images.AsNoTracking()
						.Where(i => newPhotoIds.Contains(i.Id) && i.OwnerUserId == actorUserId)
						.CountAsync(cancellationToken);

					if(count != newPhotoIds.Length)
					{
						return TaskOperationResult.Fail(TaskOperationError.ValidationFailed);
					}
				}

				var oldPhotoIds = submission.PhotoImages.Count == 0
					? new HashSet<Guid>()
					: submission.PhotoImages.Select(x => x.ImageId).ToHashSet();

				submission.SubmittedAt = model.SubmittedAt;
				submission.ProofText = model.ProofText;
				submission.DecisionStatus = TaskSubmissionDecisionStatus.SubmittedForReview;
				submission.DecidedByCoordinatorId = null;
				submission.DecidedAt = null;

				var removed = oldPhotoIds.Except(newPhotoIds).ToArray();
				if(removed.Length > 0)
				{
					await _db.TaskSubmissionImages
						.Where(x => x.SubmissionId == submission.Id && removed.Contains(x.ImageId))
						.ExecuteDeleteAsync(cancellationToken);
				}

				var added = newPhotoIds.Except(oldPhotoIds).ToArray();
				foreach(var imageId in added)
				{
					cancellationToken.ThrowIfCancellationRequested();

					_db.TaskSubmissionImages.Add(new TaskSubmissionImage
					{
						SubmissionId = submission.Id,
						ImageId = imageId,
					});
				}

				await _db.SaveChangesAsync(cancellationToken);
				return TaskOperationResult.Success();
			},
			cancellationToken,
			("ActorUserId", actorUserId),
			("SubmissionId", submissionId));
	}

	public async Task<TaskOperationResult> DeleteSubmissionAsync(
		Guid actorUserId,
		Guid taskId,
		CancellationToken cancellationToken)
	{
		return await ExecuteOperationAsync(
			DomainLogEvents.TaskSubmission.Repository.Delete,
			PersistenceLogOperations.TaskSubmissions.Delete,
			async () =>
			{
				var task = await _db.Tasks.AsNoTracking().FirstOrDefaultAsync(x => x.Id == taskId, cancellationToken);
				if(task is null)
				{
					return TaskOperationResult.Fail(TaskOperationError.TaskNotFound);
				}

				if(string.Equals(task.VerificationType, TaskVerificationType.Auto, StringComparison.Ordinal))
				{
					return TaskOperationResult.Fail(TaskOperationError.TaskAutoVerificationNotSupported);
				}

				if(task.Status == TaskStatus.Closed)
				{
					return TaskOperationResult.Fail(TaskOperationError.TaskClosed);
				}

				var existing = await _db.TaskSubmissions
					.FirstOrDefaultAsync(x => x.TaskId == taskId && x.UserId == actorUserId, cancellationToken);
				if(existing is null)
				{
					return TaskOperationResult.Fail(TaskOperationError.SubmissionNotFound);
				}

				if(existing.DecisionStatus == LdprActivistDemo.Contracts.Tasks.TaskSubmissionDecisionStatus.Approve)
				{
					return TaskOperationResult.Fail(TaskOperationError.AlreadySubmitted);
				}

				_db.TaskSubmissions.Remove(existing);
				await _db.SaveChangesAsync(cancellationToken);

				return TaskOperationResult.Success();
			},
			cancellationToken,
			("ActorUserId", actorUserId),
			("TaskId", taskId));
	}

	public async Task<TaskOperationResult> UpdateSubmissionAsync(Guid actorUserId, Guid submissionId, TaskSubmissionCreateModel model, CancellationToken cancellationToken)
	{
		return await ExecuteOperationAsync(
			DomainLogEvents.TaskSubmission.Repository.Update,
			PersistenceLogOperations.TaskSubmissions.Update,
			async () =>
			{
				var existing = await _db.TaskSubmissions
					.FirstOrDefaultAsync(x => x.Id == submissionId && x.UserId == actorUserId, cancellationToken);
				if(existing is null)
				{
					return TaskOperationResult.Fail(TaskOperationError.SubmissionNotFound);
				}

				var task = await _db.Tasks.AsNoTracking().FirstOrDefaultAsync(x => x.Id == existing.TaskId, cancellationToken);
				if(task is null)
				{
					return TaskOperationResult.Fail(TaskOperationError.TaskNotFound);
				}

				if(string.Equals(task.VerificationType, TaskVerificationType.Auto, StringComparison.Ordinal))
				{
					return TaskOperationResult.Fail(TaskOperationError.TaskAutoVerificationNotSupported);
				}

				if(string.Equals(existing.DecisionStatus, TaskSubmissionDecisionStatus.Approve, StringComparison.Ordinal))
				{
					return TaskOperationResult.Fail(TaskOperationError.AlreadySubmitted);
				}

				var isRejected = string.Equals(existing.DecisionStatus, TaskSubmissionDecisionStatus.Rejected, StringComparison.Ordinal);
				var isSubmittedForReview = string.Equals(existing.DecisionStatus, TaskSubmissionDecisionStatus.SubmittedForReview, StringComparison.Ordinal);
				if(!isRejected && !isSubmittedForReview)
				{
					return TaskOperationResult.Fail(TaskOperationError.ValidationFailed);
				}

				if(string.Equals(task.Status, TaskStatus.Closed, StringComparison.Ordinal) && !isRejected)
				{
					return TaskOperationResult.Fail(TaskOperationError.TaskClosed);
				}

				HashSet<Guid>? oldPhotoIds = null;
				Guid[]? newPhotoIds = null;

				if(model.PhotoImageIds is not null)
				{
					oldPhotoIds = await _db.TaskSubmissionImages.AsNoTracking()
						.Where(x => x.SubmissionId == existing.Id)
						.Select(x => x.ImageId)
						.ToHashSetAsync(cancellationToken);

					newPhotoIds = model.PhotoImageIds
						.Where(x => x != Guid.Empty)
						.Distinct()
						.ToArray();

					if(newPhotoIds.Length > 0)
					{
						var count = await _db.Images.AsNoTracking()
							.Where(i => newPhotoIds.Contains(i.Id) && i.OwnerUserId == actorUserId)
							.CountAsync(cancellationToken);

						if(count != newPhotoIds.Length)
						{
							return TaskOperationResult.Fail(TaskOperationError.ValidationFailed);
						}
					}
				}

				existing.SubmittedAt = model.SubmittedAt;
				existing.ProofText = model.ProofText;

				if(isRejected)
				{
					existing.DecisionStatus = TaskSubmissionDecisionStatus.SubmittedForReview;
					existing.DecidedByCoordinatorId = null;
					existing.DecidedAt = null;
				}

				if(newPhotoIds is not null)
				{
					var removed = oldPhotoIds!.Except(newPhotoIds).ToArray();
					if(removed.Length > 0)
					{
						await _db.TaskSubmissionImages
							.Where(x => x.SubmissionId == existing.Id && removed.Contains(x.ImageId))
							.ExecuteDeleteAsync(cancellationToken);
					}

					var added = newPhotoIds.Except(oldPhotoIds!).ToArray();
					foreach(var imageId in added)
					{
						cancellationToken.ThrowIfCancellationRequested();

						_db.TaskSubmissionImages.Add(new TaskSubmissionImage
						{
							SubmissionId = existing.Id,
							ImageId = imageId,
						});
					}
				}

				await _db.SaveChangesAsync(cancellationToken);

				return TaskOperationResult.Success();
			},
			cancellationToken,
			("ActorUserId", actorUserId),
			("SubmissionId", submissionId));
	}

	public async Task<TaskOperationResult<IReadOnlyList<UserPublicModel>>> GetSubmittedUsersAsync(Guid actorUserId, Guid taskId, CancellationToken cancellationToken)
	{
		return await ExecuteOperationAsync(
			DomainLogEvents.TaskSubmission.Repository.GetSubmittedUsers,
			PersistenceLogOperations.TaskSubmissions.GetSubmittedUsers,
			async () =>
			{
				var accessError = await EnsureCreatorOrTrustedAccessAsync(actorUserId, taskId, cancellationToken);
				if(accessError != TaskOperationError.None)
				{
					return TaskOperationResult<IReadOnlyList<UserPublicModel>>.Fail(accessError == TaskOperationError.Forbidden ? TaskOperationError.Forbidden : accessError);
				}

				var list = await _db.TaskSubmissions.AsNoTracking()
					.Where(x => x.TaskId == taskId
					   && (x.DecisionStatus == TaskSubmissionDecisionStatus.InProgress
						   || x.DecisionStatus == TaskSubmissionDecisionStatus.SubmittedForReview
						   || x.DecisionStatus == TaskSubmissionDecisionStatus.Rejected))
					.Join(_db.Users.AsNoTracking(),
						s => s.UserId,
						u => u.Id,
						(_, u) => new
						{
							u.Id,
							u.LastName,
							u.FirstName,
							u.MiddleName,
							u.Gender,
							u.PhoneNumber,
							u.BirthDate,
							RegionName = u.Region.Name,
							SettlementName = u.Settlement.Name,
							u.Role,
							u.IsPhoneConfirmed,
							u.AvatarImageUrl,
						})
					.OrderBy(x => x.LastName).ThenBy(x => x.FirstName).ThenBy(x => x.MiddleName)
					.Select(x => new UserPublicModel(x.Id, x.LastName, x.FirstName, x.MiddleName, x.Gender, x.PhoneNumber, x.BirthDate, x.RegionName, x.SettlementName, x.Role, x.IsPhoneConfirmed, x.AvatarImageUrl))
					.ToListAsync(cancellationToken);
				return TaskOperationResult<IReadOnlyList<UserPublicModel>>.Success(list);
			},
			cancellationToken,
			("ActorUserId", actorUserId),
			("TaskId", taskId));
	}

	public async Task<TaskOperationResult<IReadOnlyList<UserPublicModel>>> GetApprovedUsersAsync(Guid actorUserId, Guid taskId, CancellationToken cancellationToken)
	{
		return await ExecuteOperationAsync(
			DomainLogEvents.TaskSubmission.Repository.GetApprovedUsers,
			PersistenceLogOperations.TaskSubmissions.GetApprovedUsers,
			async () =>
			{
				var accessError = await EnsureCreatorOrTrustedAccessAsync(actorUserId, taskId, cancellationToken);

				if(accessError != TaskOperationError.None)
				{
					return TaskOperationResult<IReadOnlyList<UserPublicModel>>.Fail(accessError == TaskOperationError.Forbidden ? TaskOperationError.Forbidden : accessError);
				}

				var list = await _db.TaskSubmissions.AsNoTracking()
					.Where(x => x.TaskId == taskId
						&& x.DecisionStatus == LdprActivistDemo.Contracts.Tasks.TaskSubmissionDecisionStatus.Approve)
					.Join(_db.Users.AsNoTracking(),
						s => s.UserId,
						u => u.Id,
						(_, u) => new
						{
							u.Id,
							u.LastName,
							u.FirstName,
							u.MiddleName,
							u.Gender,
							u.PhoneNumber,
							u.BirthDate,
							RegionName = u.Region.Name,
							SettlementName = u.Settlement.Name,
							u.Role,
							u.IsPhoneConfirmed,
							u.AvatarImageUrl,
						})
					.OrderBy(x => x.LastName).ThenBy(x => x.FirstName).ThenBy(x => x.MiddleName)
					.Select(x => new UserPublicModel(x.Id, x.LastName, x.FirstName, x.MiddleName, x.Gender, x.PhoneNumber, x.BirthDate, x.RegionName, x.SettlementName, x.Role, x.IsPhoneConfirmed, x.AvatarImageUrl))
					.ToListAsync(cancellationToken);

				return TaskOperationResult<IReadOnlyList<UserPublicModel>>.Success(list);
			},
			cancellationToken,
			("ActorUserId", actorUserId),
			("TaskId", taskId));
	}

	public async Task<TaskOperationResult<SubmissionUserViewModel>> GetSubmittedUserAsync(Guid actorUserId, Guid taskId, Guid userId, CancellationToken cancellationToken)
	{
		return await ExecuteOperationAsync(
			DomainLogEvents.TaskSubmission.Repository.GetSubmittedUser,
			PersistenceLogOperations.TaskSubmissions.GetSubmittedUser,
			async () =>
			{
				if(actorUserId != userId)
				{
					var accessError = await EnsureCreatorOrTrustedAccessAsync(actorUserId, taskId, cancellationToken);
					if(accessError != TaskOperationError.None)
					{
						return TaskOperationResult<SubmissionUserViewModel>.Fail(accessError == TaskOperationError.Forbidden ? TaskOperationError.Forbidden : accessError);
					}
				}

				var taskMeta = await _db.Tasks.AsNoTracking()
					.Where(x => x.Id == taskId)
					.Select(x => new { x.Id, x.VerificationType })
					.FirstOrDefaultAsync(cancellationToken);

				if(taskMeta is null)
				{
					return TaskOperationResult<SubmissionUserViewModel>.Fail(TaskOperationError.TaskNotFound);
				}

				if(string.Equals(taskMeta.VerificationType, TaskVerificationType.Auto, StringComparison.Ordinal))
				{
					return TaskOperationResult<SubmissionUserViewModel>.Fail(TaskOperationError.TaskAutoVerificationNotSupported);
				}

				var submission = await _db.TaskSubmissions.AsNoTracking()
					.Include(x => x.PhotoImages)
					.FirstOrDefaultAsync(x => x.TaskId == taskId && x.UserId == userId, cancellationToken);
				if(submission is null)
				{
					return TaskOperationResult<SubmissionUserViewModel>.Fail(TaskOperationError.SubmissionNotFound);
				}

				var user = await _users.GetPublicByIdAsync(userId, cancellationToken);
				if(user is null)
				{
					return TaskOperationResult<SubmissionUserViewModel>.Fail(TaskOperationError.UserNotFound);
				}

				return TaskOperationResult<SubmissionUserViewModel>.Success(new SubmissionUserViewModel(
					user,
					ToSubmissionModel(submission)));
			},
			cancellationToken,
			("ActorUserId", actorUserId),
			("TaskId", taskId),
			("UserId", userId));
	}

	public async Task<TaskOperationResult> ApproveAsync(Guid actorUserId, Guid submissionId, DateTimeOffset decidedAt, CancellationToken cancellationToken)
	{
		return await ExecuteOperationAsync(
			DomainLogEvents.TaskSubmission.Repository.Approve,
			PersistenceLogOperations.TaskSubmissions.Approve,
			async () =>
			{
				var submission = await _db.TaskSubmissions
					.FirstOrDefaultAsync(x => x.Id == submissionId, cancellationToken);
				if(submission is null)
				{
					return TaskOperationResult.Fail(TaskOperationError.SubmissionNotFound);
				}

				var accessError = await EnsureCreatorOrTrustedAccessAsync(actorUserId, submission.TaskId, cancellationToken);
				if(accessError != TaskOperationError.None)
				{
					return TaskOperationResult.Fail(accessError == TaskOperationError.Forbidden ? TaskOperationError.Forbidden : accessError);
				}

				var taskMeta = await _db.Tasks.AsNoTracking()
					.Where(x => x.Id == submission.TaskId)
					.Select(x => new { x.VerificationType, x.Status, x.RewardPoints })
					 .FirstOrDefaultAsync(cancellationToken);

				if(taskMeta is null)
				{
					return TaskOperationResult.Fail(TaskOperationError.TaskNotFound);
				}

				if(taskMeta.RewardPoints < 0)
				{
					return TaskOperationResult.Fail(TaskOperationError.ValidationFailed);
				}

				if(string.Equals(taskMeta.Status, TaskStatus.Closed, StringComparison.Ordinal))
				{
					return TaskOperationResult.Fail(TaskOperationError.TaskClosed);
				}

				if(string.Equals(taskMeta.VerificationType, TaskVerificationType.Auto, StringComparison.Ordinal))
				{
					return TaskOperationResult.Fail(TaskOperationError.TaskAutoVerificationNotSupported);
				}

				if(submission.DecisionStatus == LdprActivistDemo.Contracts.Tasks.TaskSubmissionDecisionStatus.Approve)
				{
					return TaskOperationResult.Fail(TaskOperationError.AlreadySubmitted);
				}

				submission.DecisionStatus = LdprActivistDemo.Contracts.Tasks.TaskSubmissionDecisionStatus.Approve;
				submission.DecidedByCoordinatorId = actorUserId;
				submission.DecidedAt = decidedAt;

				if(taskMeta.RewardPoints != 0)
				{
					_db.UserPointsTransactions.Add(new UserPointsTransaction
					{
						Id = Guid.NewGuid(),
						UserId = submission.UserId,
						Amount = taskMeta.RewardPoints,
						CoordinatorUserId = actorUserId,
						TaskId = submission.TaskId,
						TransactionAt = decidedAt,
						Comment = $"Points for approval of submission {submissionId:D}.",
					});
				}

				await _db.SaveChangesAsync(cancellationToken);
				return TaskOperationResult.Success();
			},
			cancellationToken,
			("ActorUserId", actorUserId),
			("SubmissionId", submissionId));
	}

	public async Task<TaskOperationResult> RejectAsync(Guid actorUserId, Guid submissionId, DateTimeOffset decidedAt, CancellationToken cancellationToken)
	{
		return await ExecuteOperationAsync(
			DomainLogEvents.TaskSubmission.Repository.Reject,
			PersistenceLogOperations.TaskSubmissions.Reject,
			async () =>
			{
				var submission = await _db.TaskSubmissions
					.FirstOrDefaultAsync(x => x.Id == submissionId, cancellationToken);
				if(submission is null)
				{
					return TaskOperationResult.Fail(TaskOperationError.SubmissionNotFound);
				}

				var accessError = await EnsureCreatorOrTrustedAccessAsync(actorUserId, submission.TaskId, cancellationToken);
				if(accessError != TaskOperationError.None)
				{
					return TaskOperationResult.Fail(accessError == TaskOperationError.Forbidden ? TaskOperationError.Forbidden : accessError);
				}

				var taskMeta = await _db.Tasks.AsNoTracking()
					.Where(x => x.Id == submission.TaskId)
					.Select(x => new { x.VerificationType, x.Status })
					.FirstOrDefaultAsync(cancellationToken);

				if(taskMeta is null)
				{
					return TaskOperationResult.Fail(TaskOperationError.TaskNotFound);
				}

				if(string.Equals(taskMeta.Status, TaskStatus.Closed, StringComparison.Ordinal))
				{
					return TaskOperationResult.Fail(TaskOperationError.TaskClosed);
				}

				if(string.Equals(taskMeta.VerificationType, TaskVerificationType.Auto, StringComparison.Ordinal))
				{
					return TaskOperationResult.Fail(TaskOperationError.TaskAutoVerificationNotSupported);
				}

				if(submission.DecisionStatus == LdprActivistDemo.Contracts.Tasks.TaskSubmissionDecisionStatus.Approve)
				{
					return TaskOperationResult.Fail(TaskOperationError.AlreadySubmitted);
				}

				submission.DecisionStatus = LdprActivistDemo.Contracts.Tasks.TaskSubmissionDecisionStatus.Rejected;
				submission.DecidedByCoordinatorId = actorUserId;
				submission.DecidedAt = decidedAt;
				await _db.SaveChangesAsync(cancellationToken);
				return TaskOperationResult.Success();
			},
			cancellationToken,
			("ActorUserId", actorUserId),
			("SubmissionId", submissionId));
	}

	private async Task<TaskOperationResult> ExecuteOperationAsync(
		string eventName,
		string operationName,
		Func<Task<TaskOperationResult>> action,
		CancellationToken cancellationToken,
		params (string Name, object? Value)[] properties)
	{
		using var scope = _logger.BeginExecutionScope(eventName, LogLayers.PersistenceRepository, operationName, properties);
		_logger.LogStarted(eventName, LogLayers.PersistenceRepository, operationName, "Task submission repository operation started.", properties);

		try
		{
			var result = await action();
			var resultProperties = StructuredLog.Combine(properties, ("Error", result.Error));

			if(result.IsSuccess)
			{
				_logger.LogCompleted(LogLevel.Information, eventName, LogLayers.PersistenceRepository, operationName, "Task submission repository operation completed.", resultProperties);
			}
			else
			{
				_logger.LogRejected(LogLevel.Warning, eventName, LogLayers.PersistenceRepository, operationName, "Task submission repository operation rejected.", resultProperties);
			}

			return result;
		}
		catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
		{
			_logger.LogAborted(LogLevel.Information, eventName, LogLayers.PersistenceRepository, operationName, "Task submission repository operation aborted.", properties);
			throw;
		}
		catch(Exception ex)
		{
			_logger.LogFailed(LogLevel.Error, eventName, LogLayers.PersistenceRepository, operationName, "Task submission repository operation failed.", ex, properties);
			throw;
		}
	}

	private async Task<TaskOperationResult<T>> ExecuteOperationAsync<T>(
		string eventName,
		string operationName,
		Func<Task<TaskOperationResult<T>>> action,
		CancellationToken cancellationToken,
		params (string Name, object? Value)[] properties)
	{
		using var scope = _logger.BeginExecutionScope(eventName, LogLayers.PersistenceRepository, operationName, properties);
		_logger.LogStarted(eventName, LogLayers.PersistenceRepository, operationName, "Task submission repository operation started.", properties);

		try
		{
			var result = await action();
			var items = new List<(string Name, object? Value)>(properties.Length + 2);
			items.AddRange(properties);
			items.Add(("Error", result.Error));
			items.Add(("HasValue", result.Value is not null));

			if(result.Value is System.Collections.ICollection collection)
			{
				items.Add(("Count", collection.Count));
			}

			if(result.IsSuccess)
			{
				_logger.LogCompleted(LogLevel.Information, eventName, LogLayers.PersistenceRepository, operationName, "Task submission repository operation completed.", items.ToArray());
			}
			else
			{
				_logger.LogRejected(LogLevel.Warning, eventName, LogLayers.PersistenceRepository, operationName, "Task submission repository operation rejected.", items.ToArray());
			}

			return result;
		}
		catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
		{
			_logger.LogAborted(LogLevel.Information, eventName, LogLayers.PersistenceRepository, operationName, "Task submission repository operation aborted.", properties);
			throw;
		}
		catch(Exception ex)
		{
			_logger.LogFailed(LogLevel.Error, eventName, LogLayers.PersistenceRepository, operationName, "Task submission repository operation failed.", ex, properties);
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
		using var scope = _logger.BeginExecutionScope(eventName, LogLayers.PersistenceRepository, operationName, properties);
		_logger.LogStarted(eventName, LogLayers.PersistenceRepository, operationName, "Task submission repository operation started.", properties);

		try
		{
			var result = await action();
			var resultProperties = StructuredLog.Combine(
				properties,
				("Error", result.Error),
				("IsCreated", result.IsCreated));

			if(result.IsSuccess)
			{
				_logger.LogCompleted(LogLevel.Information, eventName, LogLayers.PersistenceRepository, operationName, "Task submission repository operation completed.", resultProperties);
			}
			else
			{
				_logger.LogRejected(LogLevel.Warning, eventName, LogLayers.PersistenceRepository, operationName, "Task submission repository operation rejected.", resultProperties);
			}

			return result;
		}
		catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
		{
			_logger.LogAborted(LogLevel.Information, eventName, LogLayers.PersistenceRepository, operationName, "Task submission repository operation aborted.", properties);
			throw;
		}
		catch(Exception ex)
		{
			_logger.LogFailed(LogLevel.Error, eventName, LogLayers.PersistenceRepository, operationName, "Task submission repository operation failed.", ex, properties);
			throw;
		}
	}

	public async Task<TaskOperationResult<IReadOnlyList<TaskSubmissionModel>>> GetCoordinatorFeedAsync(
		Guid actorUserId,
		Guid? taskId,
		Guid? userId,
		string? decisionStatus,
		CancellationToken cancellationToken)
	{
		var actor = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == actorUserId, cancellationToken);
		if(actor is null)
		{
			return TaskOperationResult<IReadOnlyList<TaskSubmissionModel>>.Fail(TaskOperationError.InvalidCredentials);
		}

		if(!UserRoleRules.HasCoordinatorAccess(actor.Role))
		{
			return TaskOperationResult<IReadOnlyList<TaskSubmissionModel>>.Fail(TaskOperationError.Forbidden);
		}

		if(taskId.HasValue)
		{
			var accessError = await EnsureAdminTaskAccessAsync(actorUserId, taskId.Value, cancellationToken);
			if(accessError != TaskOperationError.None)
			{
				return TaskOperationResult<IReadOnlyList<TaskSubmissionModel>>.Fail(accessError);
			}
		}

		var isAdmin = UserRoleRules.IsAdmin(actor.Role);

		IQueryable<Guid> accessibleTaskIds = _db.Tasks.AsNoTracking()
			.Where(t =>
			   isAdmin
			   || t.AuthorUserId == actorUserId
			   || _db.TaskTrustedCoordinators.AsNoTracking().Any(x => x.TaskId == t.Id && x.CoordinatorUserId == actorUserId))
		   .Where(t => t.VerificationType != TaskVerificationType.Auto)
		   .Select(t => t.Id);

		if(taskId.HasValue)
		{
			accessibleTaskIds = accessibleTaskIds.Where(x => x == taskId.Value);
		}

		IQueryable<TaskSubmission> query = _db.TaskSubmissions.AsNoTracking()
			.Where(s => accessibleTaskIds.Contains(s.TaskId))
			.Where(s => _db.Tasks.AsNoTracking().Any(t => t.Id == s.TaskId && t.VerificationType != TaskVerificationType.Auto))
			.Include(s => s.PhotoImages);

		if(userId.HasValue)
		{
			query = query.Where(s => s.UserId == userId.Value);
		}

		if(!string.IsNullOrWhiteSpace(decisionStatus))
		{
			query = ApplyDecisionStatusFilter(query, decisionStatus!);
		}

		var list = await query.ToListAsync(cancellationToken);
		return TaskOperationResult<IReadOnlyList<TaskSubmissionModel>>.Success(MapSubmissionModels(list, cancellationToken));
	}

	public async Task<TaskOperationResult<IReadOnlyList<TaskSubmissionModel>>> GetUserFeedAsync(
		Guid? taskId,
		Guid userId,
		string? decisionStatus,
		CancellationToken cancellationToken)
	{
		if(taskId.HasValue)
		{
			var taskExists = await _db.Tasks.AsNoTracking()
				.AnyAsync(x => x.Id == taskId.Value, cancellationToken);
			if(!taskExists)
			{
				return TaskOperationResult<IReadOnlyList<TaskSubmissionModel>>.Fail(TaskOperationError.TaskNotFound);
			}
		}

		IQueryable<TaskSubmission> query = _db.TaskSubmissions.AsNoTracking()
			.Where(s => s.UserId == userId)
			.Include(s => s.PhotoImages);
		if(taskId.HasValue) query = query.Where(s => s.TaskId == taskId.Value);
		if(!string.IsNullOrWhiteSpace(decisionStatus))
		{
			query = ApplyDecisionStatusFilter(query, decisionStatus!);
		}

		var list = await query.ToListAsync(cancellationToken);
		return TaskOperationResult<IReadOnlyList<TaskSubmissionModel>>.Success(MapSubmissionModels(list, cancellationToken));
	}

	public async Task<TaskOperationResult<TaskSubmissionModel>> GetByIdAsync(
		Guid actorUserId,
		Guid submissionId,
		CancellationToken cancellationToken)
	{
		var actor = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == actorUserId, cancellationToken);
		if(actor is null)
		{
			return TaskOperationResult<TaskSubmissionModel>.Fail(TaskOperationError.InvalidCredentials);
		}

		if(!UserRoleRules.HasCoordinatorAccess(actor.Role))
		{
			var ownSubmission = await _db.TaskSubmissions.AsNoTracking()
				.Include(x => x.PhotoImages)
				.FirstOrDefaultAsync(x => x.Id == submissionId && x.UserId == actorUserId, cancellationToken);

			return ownSubmission is null
			   ? TaskOperationResult<TaskSubmissionModel>.Fail(TaskOperationError.SubmissionNotFound)
			   : await MapToSubmissionModelOrAutoNotSupportedAsync(ownSubmission, cancellationToken);
		}

		var submission = await _db.TaskSubmissions.AsNoTracking()
			.Include(x => x.PhotoImages)
			.FirstOrDefaultAsync(x => x.Id == submissionId, cancellationToken);

		if(submission is null)
		{
			return TaskOperationResult<TaskSubmissionModel>.Fail(TaskOperationError.SubmissionNotFound);
		}

		if(submission.UserId == actorUserId)
		{
			return await MapToSubmissionModelOrAutoNotSupportedAsync(submission, cancellationToken);
		}

		var accessError = await EnsureAdminTaskAccessAsync(actorUserId, submission.TaskId, cancellationToken);
		if(accessError != TaskOperationError.None)
		{
			return TaskOperationResult<TaskSubmissionModel>.Fail(accessError);
		}

		return await MapToSubmissionModelOrAutoNotSupportedAsync(submission, cancellationToken);
	}

	public async Task<TaskOperationResult<IReadOnlyList<UserPublicModel>>> GetTaskUsersAsync(
		Guid actorUserId,
		Guid taskId,
		string? decisionStatus,
		CancellationToken cancellationToken)
	{
		if(taskId == Guid.Empty)
		{
			return TaskOperationResult<IReadOnlyList<UserPublicModel>>.Fail(TaskOperationError.ValidationFailed);
		}

		var actor = await _db.Users
			.AsNoTracking()
			.Where(x => x.Id == actorUserId)
			.Select(x => new
			{
				x.Id,
				x.Role,
			})
			.FirstOrDefaultAsync(cancellationToken);

		if(actor is null)
		{
			return TaskOperationResult<IReadOnlyList<UserPublicModel>>.Fail(TaskOperationError.InvalidCredentials);
		}

		var task = await _db.Tasks
			.AsNoTracking()
			.Where(x => x.Id == taskId)
			.Select(x => new
			{
				x.Id,
				x.AuthorUserId,
				x.RegionId,
				x.SettlementId,
			})
			.FirstOrDefaultAsync(cancellationToken);

		if(task is null)
		{
			return TaskOperationResult<IReadOnlyList<UserPublicModel>>.Fail(TaskOperationError.TaskNotFound);
		}

		var isAdmin = string.Equals(actor.Role, UserRoles.Admin, StringComparison.OrdinalIgnoreCase);
		var isTrustedCoordinator =
			UserRoleRules.HasCoordinatorAccess(actor.Role)
			&& await _db.TaskTrustedCoordinators
				.AsNoTracking()
				.AnyAsync(x => x.TaskId == taskId && x.CoordinatorUserId == actorUserId, cancellationToken);

		if(task.AuthorUserId != actorUserId && !isAdmin && !isTrustedCoordinator)
		{
			return TaskOperationResult<IReadOnlyList<UserPublicModel>>.Fail(TaskOperationError.Forbidden);
		}

		IQueryable<User> usersQuery;

		if(string.IsNullOrWhiteSpace(decisionStatus))
		{
			var submittedUserIds = _db.TaskSubmissions
				.AsNoTracking()
				.Where(x => x.TaskId == taskId)
				.Select(x => x.UserId)
				.Distinct();

			usersQuery = _db.Users
				.AsNoTracking()
				.Where(x => x.RegionId == task.RegionId)
				.Where(x => task.SettlementId == null || x.SettlementId == task.SettlementId.Value)
				.Where(x => !submittedUserIds.Contains(x.Id));
		}
		else
		{
			var submittedUserIds = ApplyDecisionStatusAtOrAboveFilter(
					_db.TaskSubmissions
						.AsNoTracking()
						.Where(x => x.TaskId == taskId),
					decisionStatus)
				.Select(x => x.UserId)
				.Distinct();

			usersQuery = _db.Users
				.AsNoTracking()
				.Where(x => submittedUserIds.Contains(x.Id));
		}

		var users = await usersQuery
			.OrderBy(x => x.LastName)
			.ThenBy(x => x.FirstName)
			.ThenBy(x => x.MiddleName)
			.Select(x => new UserPublicModel(
				x.Id,
				x.LastName,
				x.FirstName,
				x.MiddleName,
				x.Gender,
				x.PhoneNumber,
				x.BirthDate,
				x.Region.Name,
				x.Settlement.Name,
				x.Role,
				x.IsPhoneConfirmed,
				x.AvatarImageUrl))
			.ToListAsync(cancellationToken);

		return TaskOperationResult<IReadOnlyList<UserPublicModel>>.Success(users);
	}

	private static IQueryable<TaskSubmission> ApplyDecisionStatusFilter(IQueryable<TaskSubmission> query, string decisionStatus)
	{
		if(string.Equals(decisionStatus, TaskSubmissionDecisionStatus.InProgress, StringComparison.Ordinal))
		{
			return query.Where(s => s.DecisionStatus == TaskSubmissionDecisionStatus.InProgress);
		}

		if(string.Equals(decisionStatus, TaskSubmissionDecisionStatus.SubmittedForReview, StringComparison.Ordinal))
		{
			return query.Where(s => s.DecisionStatus == TaskSubmissionDecisionStatus.SubmittedForReview);
		}

		if(string.Equals(decisionStatus, TaskSubmissionDecisionStatus.Approve, StringComparison.Ordinal))
		{
			return query.Where(s => s.DecisionStatus == TaskSubmissionDecisionStatus.Approve);
		}

		if(string.Equals(decisionStatus, TaskSubmissionDecisionStatus.Rejected, StringComparison.Ordinal))
		{
			return query.Where(s => s.DecisionStatus == TaskSubmissionDecisionStatus.Rejected);
		}

		return query;
	}

	private static IQueryable<TaskSubmission> ApplyDecisionStatusAtOrAboveFilter(IQueryable<TaskSubmission> query, string decisionStatus)
	{
		if(string.Equals(decisionStatus, TaskSubmissionDecisionStatus.InProgress, StringComparison.Ordinal))
		{
			return query.Where(s =>
				s.DecisionStatus == TaskSubmissionDecisionStatus.InProgress
				|| s.DecisionStatus == TaskSubmissionDecisionStatus.SubmittedForReview
				|| s.DecisionStatus == TaskSubmissionDecisionStatus.Approve
				|| s.DecisionStatus == TaskSubmissionDecisionStatus.Rejected);
		}

		if(string.Equals(decisionStatus, TaskSubmissionDecisionStatus.SubmittedForReview, StringComparison.Ordinal))
		{
			return query.Where(s =>
				s.DecisionStatus == TaskSubmissionDecisionStatus.SubmittedForReview
				|| s.DecisionStatus == TaskSubmissionDecisionStatus.Approve
				|| s.DecisionStatus == TaskSubmissionDecisionStatus.Rejected);
		}

		if(string.Equals(decisionStatus, TaskSubmissionDecisionStatus.Approve, StringComparison.Ordinal))
		{
			return query.Where(s => s.DecisionStatus == TaskSubmissionDecisionStatus.Approve);
		}

		if(string.Equals(decisionStatus, TaskSubmissionDecisionStatus.Rejected, StringComparison.Ordinal))
		{
			return query.Where(s => s.DecisionStatus == TaskSubmissionDecisionStatus.Rejected);
		}

		return query.Where(_ => false);
	}

	private async Task<TaskOperationError> EnsureAdminTaskAccessAsync(Guid actorUserId, Guid taskId, CancellationToken cancellationToken)
	{
		var actor = await _db.Users.AsNoTracking()
			.Select(x => new { x.Id, x.Role })
			.FirstOrDefaultAsync(x => x.Id == actorUserId, cancellationToken);
		if(actor is null)
		{
			return TaskOperationError.InvalidCredentials;
		}

		var task = await _db.Tasks.AsNoTracking().FirstOrDefaultAsync(x => x.Id == taskId, cancellationToken);
		if(task is null)
		{
			return TaskOperationError.TaskNotFound;
		}

		if(UserRoleRules.IsAdmin(actor.Role))
		{
			return string.Equals(task.VerificationType, TaskVerificationType.Auto, StringComparison.Ordinal)
				? TaskOperationError.TaskAutoVerificationNotSupported
				: TaskOperationError.None;
		}

		if(task.AuthorUserId == actorUserId)
		{
			return string.Equals(task.VerificationType, TaskVerificationType.Auto, StringComparison.Ordinal)
				? TaskOperationError.TaskAutoVerificationNotSupported
				: TaskOperationError.None;
		}
		var isTrustedCoordinator = await _db.TaskTrustedCoordinators.AsNoTracking()
			.AnyAsync(x => x.TaskId == taskId && x.CoordinatorUserId == actorUserId, cancellationToken);
		if(!isTrustedCoordinator)
		{
			return TaskOperationError.Forbidden;
		}

		return string.Equals(task.VerificationType, TaskVerificationType.Auto, StringComparison.Ordinal)
			? TaskOperationError.TaskAutoVerificationNotSupported
			: TaskOperationError.None;
	}

	private async Task<TaskOperationError> EnsureCreatorOrTrustedAccessAsync(Guid actorUserId, Guid taskId, CancellationToken cancellationToken)
	{
		var actor = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == actorUserId, cancellationToken);
		if(actor is null)
		{
			return TaskOperationError.InvalidCredentials;
		}

		var task = await _db.Tasks.AsNoTracking().FirstOrDefaultAsync(x => x.Id == taskId, cancellationToken);
		if(task is null)
		{
			return TaskOperationError.TaskNotFound;
		}

		if(UserRoleRules.IsAdmin(actor.Role))
		{
			return string.Equals(task.VerificationType, TaskVerificationType.Auto, StringComparison.Ordinal)
				? TaskOperationError.TaskAutoVerificationNotSupported
				: TaskOperationError.None;
		}

		if(task.AuthorUserId == actorUserId)
		{
			return string.Equals(task.VerificationType, TaskVerificationType.Auto, StringComparison.Ordinal)
				? TaskOperationError.TaskAutoVerificationNotSupported
				: TaskOperationError.None;
		}

		var isTrustedCoordinator = UserRoleRules.HasCoordinatorAccess(actor.Role) && await _db.TaskTrustedCoordinators.AsNoTracking()
			.AnyAsync(x => x.TaskId == taskId && x.CoordinatorUserId == actorUserId, cancellationToken);
		if(!isTrustedCoordinator)
		{
			return TaskOperationError.Forbidden;
		}

		return string.Equals(task.VerificationType, TaskVerificationType.Auto, StringComparison.Ordinal)
			? TaskOperationError.TaskAutoVerificationNotSupported
			: TaskOperationError.None;
	}

	private static IReadOnlyList<TaskSubmissionModel> MapSubmissionModels(
		IReadOnlyList<TaskSubmission> submissions,
		CancellationToken cancellationToken)
	{
		if(submissions.Count == 0)
		{
			return Array.Empty<TaskSubmissionModel>();
		}

		var result = new List<TaskSubmissionModel>(submissions.Count);
		for(var i = 0; i < submissions.Count; i++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			result.Add(ToSubmissionModel(submissions[i]));
		}

		return result;
	}

	private static TaskSubmissionModel ToSubmissionModel(TaskSubmission s)
	{
		var photoIds = s.PhotoImages.Count == 0
			? Array.Empty<Guid>()
			: s.PhotoImages.Select(x => x.ImageId).ToArray();

		return new TaskSubmissionModel(
			s.Id,
			s.TaskId,
			s.UserId,
			s.SubmittedAt,
			s.DecisionStatus,
			s.DecidedByCoordinatorId,
			s.DecidedAt,
			photoIds,
			s.ProofText);
	}

	private async Task<TaskOperationResult<TaskSubmissionModel>> MapToSubmissionModelOrAutoNotSupportedAsync(TaskSubmission submission, CancellationToken cancellationToken)
	{
		var verificationType = await _db.Tasks.AsNoTracking()
			.Where(t => t.Id == submission.TaskId)
			.Select(t => t.VerificationType)
			.FirstOrDefaultAsync(cancellationToken);

		if(verificationType is null)
		{
			return TaskOperationResult<TaskSubmissionModel>.Fail(TaskOperationError.SubmissionNotFound);
		}

		if(string.Equals(verificationType, TaskVerificationType.Auto, StringComparison.Ordinal))
		{
			return TaskOperationResult<TaskSubmissionModel>.Fail(TaskOperationError.TaskAutoVerificationNotSupported);
		}

		return TaskOperationResult<TaskSubmissionModel>.Success(ToSubmissionModel(submission));
	}
}