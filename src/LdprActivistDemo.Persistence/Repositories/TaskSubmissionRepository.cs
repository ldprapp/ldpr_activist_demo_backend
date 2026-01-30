using LdprActivistDemo.Application.Tasks;
using LdprActivistDemo.Application.Tasks.Models;
using LdprActivistDemo.Application.Users;
using LdprActivistDemo.Application.Users.Models;
using LdprActivistDemo.Persistence.Repositories;

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

	public async Task<TaskSubmitOperationResult> SubmitAsync(Guid actorUserId, string actorUserPassword, Guid taskId, TaskSubmissionCreateModel model, CancellationToken cancellationToken)
	{
		var ok = await _users.ValidatePasswordAsync(actorUserId, actorUserPassword, cancellationToken);

		if(!ok)
		{
			_logger.LogWarning("SubmitTask rejected: invalid credentials. ActorUserId={ActorUserId}, TaskId={TaskId}.", actorUserId, taskId);
			return TaskSubmitOperationResult.Fail(TaskOperationError.InvalidCredentials);
		}

		var task = await _db.Tasks.AsNoTracking().FirstOrDefaultAsync(x => x.Id == taskId, cancellationToken);
		if(task is null)
		{
			return TaskSubmitOperationResult.Fail(TaskOperationError.TaskNotFound);
		}

		if(task.Status == TaskStatus.Closed)
		{
			_logger.LogWarning("SubmitTask rejected: task closed. ActorUserId={ActorUserId}, TaskId={TaskId}.", actorUserId, taskId);
			return TaskSubmitOperationResult.Fail(TaskOperationError.TaskClosed);
		}

		var actorLocation = await _db.Users.AsNoTracking()
			.Where(x => x.Id == actorUserId)
			.Select(x => new { x.RegionId, x.CityId })
			.FirstOrDefaultAsync(cancellationToken);

		if(actorLocation is null)
		{
			_logger.LogWarning("SubmitTask rejected: actor not found after credentials validation. ActorUserId={ActorUserId}, TaskId={TaskId}.", actorUserId, taskId);
			return TaskSubmitOperationResult.Fail(TaskOperationError.InvalidCredentials);
		}

		var isAvailableForUser = actorLocation.RegionId == task.RegionId
			&& (task.CityId is null || actorLocation.CityId == task.CityId);

		if(!isAvailableForUser)
		{
			_logger.LogWarning(
				"SubmitTask rejected: task not available for user location. ActorUserId={ActorUserId}, TaskId={TaskId}, UserRegionId={UserRegionId}, UserCityId={UserCityId}, TaskRegionId={TaskRegionId}, TaskCityId={TaskCityId}.",
				actorUserId,
				taskId,
				actorLocation.RegionId,
				actorLocation.CityId,
				task.RegionId,
				task.CityId);
			return TaskSubmitOperationResult.Fail(TaskOperationError.Forbidden);
		}

		var existing = await _db.TaskSubmissions
		   .AsNoTracking()
		   .FirstOrDefaultAsync(x => x.TaskId == taskId && x.UserId == actorUserId, cancellationToken);

		if(existing is not null)
		{
			return TaskSubmitOperationResult.Fail(existing.ConfirmedAt is null
				? TaskOperationError.SubmissionAlreadyExists
				: TaskOperationError.AlreadySubmitted);
		}

		var photoIds = (model.PhotoImageIds ?? Array.Empty<Guid>())
			.Where(x => x != Guid.Empty)
			.Distinct()
			.ToArray();

		if(photoIds.Length > 0)
		{
			var count = await _db.Images.AsNoTracking()
				.Where(i => photoIds.Contains(i.Id))
				.CountAsync(cancellationToken);

			if(count != photoIds.Length)
			{
				_logger.LogWarning(
					"SubmitTask rejected: some PhotoImageIds not found. ActorUserId={ActorUserId}, TaskId={TaskId}.",
					actorUserId,
					taskId);
				return TaskSubmitOperationResult.Fail(TaskOperationError.ValidationFailed);
			}
		}

		var submission = new TaskSubmission
		{
			Id = Guid.NewGuid(),
			TaskId = taskId,
			UserId = actorUserId,
			SubmittedAt = model.SubmittedAt,
			ProofText = model.ProofText,
		};

		_db.TaskSubmissions.Add(submission);

		foreach(var imageId in photoIds)
		{
			_db.TaskSubmissionImages.Add(new TaskSubmissionImage
			{
				SubmissionId = submission.Id,
				ImageId = imageId,
			});
		}

		await _db.SaveChangesAsync(cancellationToken);
		return TaskSubmitOperationResult.Created();
	}

	public async Task<TaskOperationResult> UpdateSubmissionAsync(Guid actorUserId, string actorUserPassword, Guid taskId, TaskSubmissionCreateModel model, CancellationToken cancellationToken)
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
		if(existing.ConfirmedAt is not null)
		{
			return TaskOperationResult.Fail(TaskOperationError.AlreadySubmitted);
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
						taskId);
					return TaskOperationResult.Fail(TaskOperationError.ValidationFailed);
				}
			}
		}

		existing.SubmittedAt = model.SubmittedAt;
		existing.ProofText = model.ProofText;

		if(newPhotoIds is not null)
		{
			await _db.TaskSubmissionImages
				.Where(x => x.SubmissionId == existing.Id)
				.ExecuteDeleteAsync(cancellationToken);

			foreach(var imageId in newPhotoIds)
			{
				_db.TaskSubmissionImages.Add(new TaskSubmissionImage
				{
					SubmissionId = existing.Id,
					ImageId = imageId,
				});
			}
		}

		await _db.SaveChangesAsync(cancellationToken);

		if(oldPhotoIds is not null && newPhotoIds is not null)
		{
			var removed = oldPhotoIds.Except(newPhotoIds).ToArray();
			if(removed.Length > 0)
			{
				await ImageGcHelpers.DeleteOrphanManyAsync(_db, removed, cancellationToken);
			}
		}

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
			.Where(x => x.TaskId == taskId && x.ConfirmedAt == null)
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
				})
			.OrderBy(x => x.LastName).ThenBy(x => x.FirstName).ThenBy(x => x.MiddleName)
			.Select(x => new UserPublicModel(x.Id, x.LastName, x.FirstName, x.MiddleName, x.Gender, x.PhoneNumber, x.BirthDate, x.RegionId, x.CityId, x.IsPhoneConfirmed, x.Points))
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
			.Where(x => x.TaskId == taskId && x.ConfirmedAt != null)
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
				})
			.OrderBy(x => x.LastName).ThenBy(x => x.FirstName).ThenBy(x => x.MiddleName)
			.Select(x => new UserPublicModel(x.Id, x.LastName, x.FirstName, x.MiddleName, x.Gender, x.PhoneNumber, x.BirthDate, x.RegionId, x.CityId, x.IsPhoneConfirmed, x.Points))
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

	public async Task<TaskOperationResult> ApproveAsync(Guid actorUserId, string actorUserPassword, Guid taskId, Guid userId, DateTimeOffset confirmedAt, CancellationToken cancellationToken)
	{
		var accessError = await EnsureCreatorOrTrustedAccessAsync(actorUserId, actorUserPassword, taskId, cancellationToken);
		if(accessError != TaskOperationError.None)
		{
			return TaskOperationResult.Fail(accessError == TaskOperationError.Forbidden ? TaskOperationError.Forbidden : accessError);
		}

		var submission = await _db.TaskSubmissions
			.FirstOrDefaultAsync(x => x.TaskId == taskId && x.UserId == userId, cancellationToken);
		if(submission is null)
		{
			return TaskOperationResult.Fail(TaskOperationError.SubmissionNotFound);
		}
		if(submission.ConfirmedAt is not null)
		{
			return TaskOperationResult.Fail(TaskOperationError.AlreadySubmitted);
		}

		submission.ConfirmedByAdminId = actorUserId;
		submission.ConfirmedAt = confirmedAt;
		await _db.SaveChangesAsync(cancellationToken);
		return TaskOperationResult.Success();
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
			s.ConfirmedByAdminId,
			s.ConfirmedAt,
			photoIds,
			s.ProofText);
	}
}