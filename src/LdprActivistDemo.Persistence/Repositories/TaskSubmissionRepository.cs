using LdprActivistDemo.Application.Tasks;
using LdprActivistDemo.Application.Tasks.Models;
using LdprActivistDemo.Application.Users;
using LdprActivistDemo.Application.Users.Models;
using LdprActivistDemo.Contracts.Tasks;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using TaskStatus = LdprActivistDemo.Contracts.Tasks.TaskStatus;

namespace LdprActivistDemo.Persistence;

public sealed class TaskSubmissionRepository : ITaskSubmissionRepository
{
	private readonly AppDbContext _db;
	private readonly IUserRepository _users;
	private readonly IPasswordHasher _passwordHasher;
	private readonly ILogger<TaskSubmissionRepository> _logger;

	public TaskSubmissionRepository(AppDbContext db, IUserRepository users, IPasswordHasher passwordHasher, ILogger<TaskSubmissionRepository> logger)
	{
		_db = db;
		_users = users;
		_passwordHasher = passwordHasher;
		_logger = logger;
	}

	public async Task<TaskOperationResult> ValidateActorAsync(
		Guid actorUserId,
		string actorUserPassword,
		CancellationToken cancellationToken)
	{
		var ok = await _users.ValidatePasswordAsync(actorUserId, actorUserPassword, cancellationToken);
		return ok
			? TaskOperationResult.Success()
			: TaskOperationResult.Fail(TaskOperationError.InvalidCredentials);
	}

	public async Task<TaskSubmitOperationResult> SubmitAsync(Guid actorUserId, string actorUserPassword, Guid taskId, TaskSubmissionCreateModel model, CancellationToken cancellationToken)
	{
		var actor = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == actorUserId, cancellationToken);
		if(actor is null || !_passwordHasher.Verify(actor.PasswordHash, actorUserPassword))
		{
			_logger.LogWarning("SubmitTask rejected: invalid credentials. ActorUserId={ActorUserId}, TaskId={TaskId}.", actorUserId, taskId);
			return TaskSubmitOperationResult.Fail(TaskOperationError.InvalidCredentials);
		}

		var task = await _db.Tasks.AsNoTracking().FirstOrDefaultAsync(x => x.Id == taskId, cancellationToken);
		if(task is null)
		{
			return TaskSubmitOperationResult.Fail(TaskOperationError.TaskNotFound);
		}

		if(string.Equals(task.VerificationType, TaskVerificationType.Auto, StringComparison.Ordinal))
		{
			_logger.LogWarning("SubmitTask rejected: task has auto verification type. ActorUserId={ActorUserId}, TaskId={TaskId}.",
				actorUserId,
				taskId);
			return TaskSubmitOperationResult.Fail(TaskOperationError.TaskAutoVerificationNotSupported);
		}

		if(string.Equals(task.Status, TaskStatus.Closed, StringComparison.Ordinal))
		{
			_logger.LogWarning("SubmitTask rejected: task closed. ActorUserId={ActorUserId}, TaskId={TaskId}.", actorUserId, taskId);
			return TaskSubmitOperationResult.Fail(TaskOperationError.TaskClosed);
		}

		var geoOk = task.RegionId == actor.RegionId
			&& (task.CityId is null || actor.CityId == task.CityId.Value);
		if(!geoOk)
		{
			_logger.LogWarning("SubmitTask rejected: task is not accessible by geo. ActorUserId={ActorUserId}, TaskId={TaskId}.", actorUserId, taskId);
			return TaskSubmitOperationResult.Fail(TaskOperationError.TaskAccessDenied);
		}

		if(actor.IsAdmin)
		{
			var isAuthor = task.AuthorUserId == actorUserId;
			var isTrustedAdmin = await _db.TaskTrustedAdmins.AsNoTracking()
				.AnyAsync(x => x.TaskId == taskId && x.AdminUserId == actorUserId, cancellationToken);
			if(isAuthor || isTrustedAdmin)
			{
				_logger.LogWarning(
					"SubmitTask rejected: admin cannot submit to own/responsible task. ActorUserId={ActorUserId}, TaskId={TaskId}.",
					actorUserId,
					taskId);
				return TaskSubmitOperationResult.Fail(TaskOperationError.TaskAccessDenied);
			}
		}

		var existing = await _db.TaskSubmissions
		   .AsNoTracking()
		   .FirstOrDefaultAsync(x => x.TaskId == taskId && x.UserId == actorUserId, cancellationToken);

		if(existing is not null)
		{
			return TaskSubmitOperationResult.Fail(existing.DecisionStatus == TaskSubmissionDecisionStatus.Approve
				? TaskOperationError.AlreadySubmitted
				: TaskOperationError.SubmissionAlreadyExists);
		}

		var submission = new TaskSubmission
		{
			Id = Guid.NewGuid(),
			TaskId = taskId,
			UserId = actorUserId,
			SubmittedAt = model.SubmittedAt,
			DecisionStatus = TaskSubmissionDecisionStatus.InProgress,
			ProofText = null,
		};

		_db.TaskSubmissions.Add(submission);

		await _db.SaveChangesAsync(cancellationToken);
		return TaskSubmitOperationResult.Created();
	}

	public async Task<TaskOperationResult> SubmitForReviewAsync(Guid actorUserId, string actorUserPassword, Guid submissionId, TaskSubmissionCreateModel model, CancellationToken cancellationToken)
	{
		var ok = await _users.ValidatePasswordAsync(actorUserId, actorUserPassword, cancellationToken);
		if(!ok)
		{
			return TaskOperationResult.Fail(TaskOperationError.InvalidCredentials);
		}

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
				.Where(i => newPhotoIds.Contains(i.Id))
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
		submission.DecidedByAdminId = null;
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
			_db.TaskSubmissionImages.Add(new TaskSubmissionImage
			{
				SubmissionId = submission.Id,
				ImageId = imageId,
			});
		}

		await _db.SaveChangesAsync(cancellationToken);
		return TaskOperationResult.Success();
	}

	public async Task<TaskOperationResult> DeleteSubmissionAsync(
		Guid actorUserId,
		string actorUserPassword,
		Guid taskId,
		CancellationToken cancellationToken)
	{
		var ok = await _users.ValidatePasswordAsync(actorUserId, actorUserPassword, cancellationToken);
		if(!ok)
		{
			return TaskOperationResult.Fail(TaskOperationError.InvalidCredentials);
		}

		var task = await _db.Tasks.AsNoTracking().FirstOrDefaultAsync(x => x.Id == taskId, cancellationToken);
		if(task is null)
		{
			return TaskOperationResult.Fail(TaskOperationError.TaskNotFound);
		}

		if(string.Equals(task.VerificationType, TaskVerificationType.Auto, StringComparison.Ordinal))
		{
			_logger.LogWarning("DeleteSubmission rejected: task has auto verification type. ActorUserId={ActorUserId}, TaskId={TaskId}.",
				actorUserId,
				taskId);
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
	}

	public async Task<TaskOperationResult> UpdateSubmissionAsync(Guid actorUserId, string actorUserPassword, Guid submissionId, TaskSubmissionCreateModel model, CancellationToken cancellationToken)
	{
		var ok = await _users.ValidatePasswordAsync(actorUserId, actorUserPassword, cancellationToken);
		if(!ok)
		{
			return TaskOperationResult.Fail(TaskOperationError.InvalidCredentials);
		}

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
			_logger.LogWarning("UpdateSubmission rejected: task has auto verification type. ActorUserId={ActorUserId}, TaskId={TaskId}.",
				actorUserId,
				existing.TaskId);
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
					.Where(i => newPhotoIds.Contains(i.Id))
					.CountAsync(cancellationToken);

				if(count != newPhotoIds.Length)
				{
					_logger.LogWarning(
						"UpdateSubmission rejected: some PhotoImageIds not found. ActorUserId={ActorUserId}, TaskId={TaskId}.",
						actorUserId,
						existing.TaskId);
					return TaskOperationResult.Fail(TaskOperationError.ValidationFailed);
				}
			}
		}

		existing.SubmittedAt = model.SubmittedAt;
		existing.ProofText = model.ProofText;

		if(isRejected)
		{
			existing.DecisionStatus = TaskSubmissionDecisionStatus.SubmittedForReview;
			existing.DecidedByAdminId = null;
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
				_db.TaskSubmissionImages.Add(new TaskSubmissionImage
				{
					SubmissionId = existing.Id,
					ImageId = imageId,
				});
			}
		}

		await _db.SaveChangesAsync(cancellationToken);

		return TaskOperationResult.Success();
	}

	public async Task<TaskOperationResult<IReadOnlyList<UserPublicModel>>> GetSubmittedUsersAsync(Guid actorUserId, string actorUserPassword, Guid taskId, CancellationToken cancellationToken)
	{
		var accessError = await EnsureCreatorOrTrustedAccessAsync(actorUserId, actorUserPassword, taskId, cancellationToken);
		if(accessError != TaskOperationError.None)
		{
			_logger.LogWarning("GetSubmittedUsers rejected: access denied. ActorUserId={ActorUserId}, TaskId={TaskId}, Error={Error}.",
				actorUserId, taskId, accessError);
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
					u.RegionId,
					u.CityId,
					u.IsPhoneConfirmed,
					u.Points,
					u.AvatarImageUrl,
				})
			.OrderBy(x => x.LastName).ThenBy(x => x.FirstName).ThenBy(x => x.MiddleName)
			.Select(x => new UserPublicModel(x.Id, x.LastName, x.FirstName, x.MiddleName, x.Gender, x.PhoneNumber, x.BirthDate, x.RegionId, x.CityId, x.IsPhoneConfirmed, x.Points, x.AvatarImageUrl))
			.ToListAsync(cancellationToken);
		return TaskOperationResult<IReadOnlyList<UserPublicModel>>.Success(list);
	}

	public async Task<TaskOperationResult<IReadOnlyList<UserPublicModel>>> GetApprovedUsersAsync(Guid actorUserId, string actorUserPassword, Guid taskId, CancellationToken cancellationToken)
	{
		var accessError = await EnsureCreatorOrTrustedAccessAsync(actorUserId, actorUserPassword, taskId, cancellationToken);

		if(accessError != TaskOperationError.None)
		{
			_logger.LogWarning("GetApprovedUsers rejected: access denied. ActorUserId={ActorUserId}, TaskId={TaskId}, Error={Error}.",
				actorUserId, taskId, accessError);
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
					u.RegionId,
					u.CityId,
					u.IsPhoneConfirmed,
					u.Points,
					u.AvatarImageUrl,
				})
			.OrderBy(x => x.LastName).ThenBy(x => x.FirstName).ThenBy(x => x.MiddleName)
			.Select(x => new UserPublicModel(x.Id, x.LastName, x.FirstName, x.MiddleName, x.Gender, x.PhoneNumber, x.BirthDate, x.RegionId, x.CityId, x.IsPhoneConfirmed, x.Points, x.AvatarImageUrl))
			.ToListAsync(cancellationToken);

		return TaskOperationResult<IReadOnlyList<UserPublicModel>>.Success(list);
	}

	public async Task<TaskOperationResult<SubmissionUserViewModel>> GetSubmittedUserAsync(Guid actorUserId, string actorUserPassword, Guid taskId, Guid userId, CancellationToken cancellationToken)
	{
		if(actorUserId == userId)
		{
			var selfOk = await _users.ValidatePasswordAsync(actorUserId, actorUserPassword, cancellationToken);
			if(!selfOk)
			{
				return TaskOperationResult<SubmissionUserViewModel>.Fail(TaskOperationError.InvalidCredentials);
			}
		}
		else
		{
			var accessError = await EnsureCreatorOrTrustedAccessAsync(actorUserId, actorUserPassword, taskId, cancellationToken);
			if(accessError != TaskOperationError.None)
			{
				return TaskOperationResult<SubmissionUserViewModel>.Fail(accessError == TaskOperationError.Forbidden ? TaskOperationError.Forbidden : accessError);
			}
		}

		var taskExists = await _db.Tasks.AsNoTracking().AnyAsync(x => x.Id == taskId, cancellationToken);
		if(!taskExists)
		{
			return TaskOperationResult<SubmissionUserViewModel>.Fail(TaskOperationError.TaskNotFound);
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
	}

	public async Task<TaskOperationResult> ApproveAsync(Guid actorUserId, string actorUserPassword, Guid submissionId, DateTimeOffset decidedAt, CancellationToken cancellationToken)
	{
		var submission = await _db.TaskSubmissions
			.FirstOrDefaultAsync(x => x.Id == submissionId, cancellationToken);
		if(submission is null)
		{
			return TaskOperationResult.Fail(TaskOperationError.SubmissionNotFound);
		}

		var accessError = await EnsureCreatorOrTrustedAccessAsync(actorUserId, actorUserPassword, submission.TaskId, cancellationToken);
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
			_logger.LogWarning("Approve rejected: task has auto verification type. ActorUserId={ActorUserId}, TaskId={TaskId}.",
				actorUserId,
				submission.TaskId);
			return TaskOperationResult.Fail(TaskOperationError.TaskAutoVerificationNotSupported);
		}

		if(submission.DecisionStatus == LdprActivistDemo.Contracts.Tasks.TaskSubmissionDecisionStatus.Approve)
		{
			return TaskOperationResult.Fail(TaskOperationError.AlreadySubmitted);
		}

		submission.DecisionStatus = LdprActivistDemo.Contracts.Tasks.TaskSubmissionDecisionStatus.Approve;
		submission.DecidedByAdminId = actorUserId;
		submission.DecidedAt = decidedAt;
		await _db.SaveChangesAsync(cancellationToken);
		return TaskOperationResult.Success();
	}

	public async Task<TaskOperationResult> RejectAsync(Guid actorUserId, string actorUserPassword, Guid submissionId, DateTimeOffset decidedAt, CancellationToken cancellationToken)
	{
		var submission = await _db.TaskSubmissions
			.FirstOrDefaultAsync(x => x.Id == submissionId, cancellationToken);
		if(submission is null)
		{
			return TaskOperationResult.Fail(TaskOperationError.SubmissionNotFound);
		}

		var accessError = await EnsureCreatorOrTrustedAccessAsync(actorUserId, actorUserPassword, submission.TaskId, cancellationToken);
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
			_logger.LogWarning("Reject rejected: task has auto verification type. ActorUserId={ActorUserId}, TaskId={TaskId}.",
				actorUserId,
				submission.TaskId);
			return TaskOperationResult.Fail(TaskOperationError.TaskAutoVerificationNotSupported);
		}

		if(submission.DecisionStatus == LdprActivistDemo.Contracts.Tasks.TaskSubmissionDecisionStatus.Approve)
		{
			return TaskOperationResult.Fail(TaskOperationError.AlreadySubmitted);
		}

		submission.DecisionStatus = LdprActivistDemo.Contracts.Tasks.TaskSubmissionDecisionStatus.Rejected;
		submission.DecidedByAdminId = actorUserId;
		submission.DecidedAt = decidedAt;
		await _db.SaveChangesAsync(cancellationToken);
		return TaskOperationResult.Success();
	}

	public async Task<TaskOperationResult<IReadOnlyList<TaskSubmissionModel>>> GetAdminFeedAsync(
		Guid actorUserId,
		string actorUserPassword,
		Guid? taskId,
		Guid? userId,
		string? decisionStatus,
		CancellationToken cancellationToken)
	{
		var actor = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == actorUserId, cancellationToken);
		if(actor is null || !_passwordHasher.Verify(actor.PasswordHash, actorUserPassword))
		{
			return TaskOperationResult<IReadOnlyList<TaskSubmissionModel>>.Fail(TaskOperationError.InvalidCredentials);
		}

		if(!actor.IsAdmin)
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

		IQueryable<Guid> accessibleTaskIds = _db.Tasks.AsNoTracking()
			.Where(t =>
				t.AuthorUserId == actorUserId
				|| _db.TaskTrustedAdmins.AsNoTracking().Any(x => x.TaskId == t.Id && x.AdminUserId == actorUserId))
			.Select(t => t.Id);

		if(taskId.HasValue)
		{
			accessibleTaskIds = accessibleTaskIds.Where(x => x == taskId.Value);
		}

		IQueryable<TaskSubmission> query = _db.TaskSubmissions.AsNoTracking()
			.Where(s => accessibleTaskIds.Contains(s.TaskId))
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
		var mapped = list.Select(ToSubmissionModel).ToList();
		return TaskOperationResult<IReadOnlyList<TaskSubmissionModel>>.Success(mapped);
	}

	public async Task<TaskOperationResult<IReadOnlyList<TaskSubmissionModel>>> GetUserFeedAsync(
		Guid actorUserId,
		string actorUserPassword,
		string? decisionStatus,
		CancellationToken cancellationToken)
	{
		var ok = await _users.ValidatePasswordAsync(actorUserId, actorUserPassword, cancellationToken);
		if(!ok)
		{
			return TaskOperationResult<IReadOnlyList<TaskSubmissionModel>>.Fail(TaskOperationError.InvalidCredentials);
		}

		IQueryable<TaskSubmission> query = _db.TaskSubmissions.AsNoTracking()
			.Where(s => s.UserId == actorUserId)
			.Include(s => s.PhotoImages);

		if(!string.IsNullOrWhiteSpace(decisionStatus))
		{
			query = ApplyDecisionStatusFilter(query, decisionStatus!);
		}

		var list = await query.ToListAsync(cancellationToken);
		var mapped = list.Select(ToSubmissionModel).ToList();
		return TaskOperationResult<IReadOnlyList<TaskSubmissionModel>>.Success(mapped);
	}

	public async Task<TaskOperationResult<TaskSubmissionModel>> GetByIdAsync(
		Guid actorUserId,
		string actorUserPassword,
		Guid submissionId,
		CancellationToken cancellationToken)
	{
		var actor = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == actorUserId, cancellationToken);
		if(actor is null || !_passwordHasher.Verify(actor.PasswordHash, actorUserPassword))
		{
			return TaskOperationResult<TaskSubmissionModel>.Fail(TaskOperationError.InvalidCredentials);
		}

		if(!actor.IsAdmin)
		{
			var ownSubmission = await _db.TaskSubmissions.AsNoTracking()
				.Include(x => x.PhotoImages)
				.FirstOrDefaultAsync(x => x.Id == submissionId && x.UserId == actorUserId, cancellationToken);

			return ownSubmission is null
				? TaskOperationResult<TaskSubmissionModel>.Fail(TaskOperationError.SubmissionNotFound)
				: TaskOperationResult<TaskSubmissionModel>.Success(ToSubmissionModel(ownSubmission));
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
			return TaskOperationResult<TaskSubmissionModel>.Success(ToSubmissionModel(submission));
		}

		var accessError = await EnsureAdminTaskAccessAsync(actorUserId, submission.TaskId, cancellationToken);
		if(accessError != TaskOperationError.None)
		{
			return TaskOperationResult<TaskSubmissionModel>.Fail(accessError);
		}

		return TaskOperationResult<TaskSubmissionModel>.Success(ToSubmissionModel(submission));
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

	private async Task<TaskOperationError> EnsureAdminTaskAccessAsync(Guid actorUserId, Guid taskId, CancellationToken cancellationToken)
	{
		var task = await _db.Tasks.AsNoTracking().FirstOrDefaultAsync(x => x.Id == taskId, cancellationToken);
		if(task is null)
		{
			return TaskOperationError.TaskNotFound;
		}

		if(task.AuthorUserId == actorUserId)
		{
			return TaskOperationError.None;
		}

		var isTrustedAdmin = await _db.TaskTrustedAdmins.AsNoTracking()
			.AnyAsync(x => x.TaskId == taskId && x.AdminUserId == actorUserId, cancellationToken);

		return isTrustedAdmin ? TaskOperationError.None : TaskOperationError.Forbidden;
	}

	private async Task<TaskOperationError> EnsureCreatorOrTrustedAccessAsync(Guid actorUserId, string actorUserPassword, Guid taskId, CancellationToken cancellationToken)
	{
		var actor = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == actorUserId, cancellationToken);
		if(actor is null || !_passwordHasher.Verify(actor.PasswordHash, actorUserPassword))
		{
			return TaskOperationError.InvalidCredentials;
		}

		var task = await _db.Tasks.AsNoTracking().FirstOrDefaultAsync(x => x.Id == taskId, cancellationToken);
		if(task is null)
		{
			return TaskOperationError.TaskNotFound;
		}

		if(task.AuthorUserId == actorUserId)
		{
			return TaskOperationError.None;
		}

		var isTrustedAdmin = actor.IsAdmin && await _db.TaskTrustedAdmins.AsNoTracking()
			.AnyAsync(x => x.TaskId == taskId && x.AdminUserId == actorUserId, cancellationToken);
		return isTrustedAdmin ? TaskOperationError.None : TaskOperationError.Forbidden;
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
			s.DecidedByAdminId,
			s.DecidedAt,
			photoIds,
			s.ProofText);
	}
}