using LdprActivistDemo.Application.Tasks;
using LdprActivistDemo.Application.Tasks.Models;
using LdprActivistDemo.Application.Users;
using LdprActivistDemo.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using TaskStatus = LdprActivistDemo.Contracts.Tasks.TaskStatus;

namespace LdprActivistDemo.Persistence;

public sealed class TaskRepository : ITaskRepository
{
	private readonly AppDbContext _db;
	private readonly IPasswordHasher _passwordHasher;
	private readonly ILogger<TaskRepository> _logger;

	public TaskRepository(AppDbContext db, IPasswordHasher passwordHasher, ILogger<TaskRepository> logger)
	{
		_db = db;
		_passwordHasher = passwordHasher;
		_logger = logger;
	}

	public async Task<TaskOperationResult<Guid>> CreateAsync(Guid actorUserId, string actorUserPassword, TaskCreateModel model, CancellationToken cancellationToken)
	{
		var actor = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == actorUserId, cancellationToken);

		if(actor is null || !_passwordHasher.Verify(actor.PasswordHash, actorUserPassword))
		{
			_logger.LogWarning("CreateTask rejected: invalid credentials. ActorUserId={ActorUserId}.", actorUserId);
			return TaskOperationResult<Guid>.Fail(TaskOperationError.InvalidCredentials);
		}

		if(!actor.IsAdmin)
		{
			_logger.LogWarning("CreateTask rejected: actor is not admin. ActorUserId={ActorUserId}.", actorUserId);
			return TaskOperationResult<Guid>.Fail(TaskOperationError.Forbidden);
		}

		var geoError = await EnsureRegionCityAsync(model.RegionId, model.CityId, cancellationToken);

		if(geoError != TaskOperationError.None)
		{
			_logger.LogWarning("CreateTask rejected: invalid geo. ActorUserId={ActorUserId}, RegionId={RegionId}, CityId={CityId}, Error={Error}.",
				actorUserId, model.RegionId, model.CityId, geoError);
			return TaskOperationResult<Guid>.Fail(geoError);
		}

		if(model.RewardPoints < 0)
		{
			_logger.LogWarning("CreateTask rejected: negative RewardPoints. ActorUserId={ActorUserId}, RewardPoints={RewardPoints}.",
				actorUserId, model.RewardPoints);
			return TaskOperationResult<Guid>.Fail(TaskOperationError.ValidationFailed);
		}

		var trustedAdminIds = model.TrustedAdminIds
			.Where(x => x != Guid.Empty)
			.Distinct()
			.ToArray();

		if(trustedAdminIds.Length > 0)
		{
			var existingAdminIds = await _db.Users.AsNoTracking()
				.Where(u => trustedAdminIds.Contains(u.Id) && u.IsAdmin)
				.Select(u => u.Id)
				.ToListAsync(cancellationToken);

			if(existingAdminIds.Count != trustedAdminIds.Length)
			{
				_logger.LogWarning("CreateTask rejected: some TrustedAdminIds not found or not admins. ActorUserId={ActorUserId}.",
					actorUserId);
				return TaskOperationResult<Guid>.Fail(TaskOperationError.UserNotFound);
			}
		}

		var entity = new TaskEntity
		{
			Id = Guid.NewGuid(),
			AuthorUserId = actorUserId,
			Title = model.Title,
			Description = model.Description,
			RequirementsText = model.RequirementsText ?? string.Empty,
			RewardPoints = model.RewardPoints,
			CoverImageId = model.CoverImageId.HasValue && model.CoverImageId.Value != Guid.Empty
				? model.CoverImageId
				: null,
			ExecutionLocation = model.ExecutionLocation,
			PublishedAt = model.PublishedAt,
			DeadlineAt = model.DeadlineAt ?? model.PublishedAt,
			Status = model.Status,
			RegionId = model.RegionId,
			CityId = model.CityId,
		};

		_db.Tasks.Add(entity);

		foreach(var adminId in trustedAdminIds)
		{
			_db.TaskTrustedAdmins.Add(new TaskTrustedAdmin
			{
				TaskId = entity.Id,
				AdminUserId = adminId,
			});
		}

		await _db.SaveChangesAsync(cancellationToken);
		return TaskOperationResult<Guid>.Success(entity.Id);
	}

	public async Task<TaskOperationResult> UpdateAsync(Guid actorUserId, string actorUserPassword, Guid taskId, TaskUpdateModel model, CancellationToken cancellationToken)
	{
		var actor = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == actorUserId, cancellationToken);

		if(actor is null || !_passwordHasher.Verify(actor.PasswordHash, actorUserPassword))
		{
			_logger.LogWarning("UpdateTask rejected: invalid credentials. ActorUserId={ActorUserId}, TaskId={TaskId}.", actorUserId, taskId);
			return TaskOperationResult.Fail(TaskOperationError.InvalidCredentials);
		}

		var task = await _db.Tasks.FirstOrDefaultAsync(x => x.Id == taskId, cancellationToken);
		if(task is null)
		{
			return TaskOperationResult.Fail(TaskOperationError.TaskNotFound);
		}

		var isAuthor = task.AuthorUserId == actorUserId;

		if(!isAuthor)
		{
			var isTrustedAdmin = actor.IsAdmin && await _db.TaskTrustedAdmins.AsNoTracking()
				.AnyAsync(x => x.TaskId == taskId && x.AdminUserId == actorUserId, cancellationToken);

			if(!isTrustedAdmin)
			{
				_logger.LogWarning(
					"UpdateTask rejected: actor has no edit rights (not author and not trusted admin). ActorUserId={ActorUserId}, TaskId={TaskId}, AuthorUserId={AuthorUserId}, IsAdmin={IsAdmin}.",
					actorUserId,
					taskId,
					task.AuthorUserId,
					actor.IsAdmin);
				return TaskOperationResult.Fail(TaskOperationError.Forbidden);
			}
		}

		var geoError = await EnsureRegionCityAsync(model.RegionId, model.CityId, cancellationToken);

		if(geoError != TaskOperationError.None)
		{
			_logger.LogWarning("UpdateTask rejected: invalid geo. ActorUserId={ActorUserId}, TaskId={TaskId}, RegionId={RegionId}, CityId={CityId}, Error={Error}.",
				actorUserId, taskId, model.RegionId, model.CityId, geoError);
			return TaskOperationResult.Fail(geoError);
		}

		if(model.RewardPoints < 0)
		{
			_logger.LogWarning("UpdateTask rejected: negative RewardPoints. ActorUserId={ActorUserId}, TaskId={TaskId}, RewardPoints={RewardPoints}.",
				actorUserId, taskId, model.RewardPoints);
			return TaskOperationResult.Fail(TaskOperationError.ValidationFailed);
		}

		var trustedAdminIds = model.TrustedAdminIds
			.Where(x => x != Guid.Empty)
			.Distinct()
			.ToArray();

		if(trustedAdminIds.Length > 0)
		{
			var existingAdminIds = await _db.Users.AsNoTracking()
				.Where(u => trustedAdminIds.Contains(u.Id) && u.IsAdmin)
				.Select(u => u.Id)
				.ToListAsync(cancellationToken);

			if(existingAdminIds.Count != trustedAdminIds.Length)
			{
				_logger.LogWarning("UpdateTask rejected: some TrustedAdminIds not found or not admins. ActorUserId={ActorUserId}, TaskId={TaskId}.",
					actorUserId, taskId);
				return TaskOperationResult.Fail(TaskOperationError.UserNotFound);
			}
		}

		var previousCoverId = task.CoverImageId;
		var coverChanged = false;

		task.Title = model.Title;
		task.Description = model.Description;
		task.RequirementsText = model.RequirementsText ?? string.Empty;
		task.RewardPoints = model.RewardPoints;

		if(model.CoverImageId.HasValue)
		{
			coverChanged = true;
			task.CoverImageId = model.CoverImageId.Value == Guid.Empty ? null : model.CoverImageId.Value;
		}

		task.ExecutionLocation = model.ExecutionLocation;
		task.PublishedAt = model.PublishedAt;
		task.DeadlineAt = model.DeadlineAt ?? model.PublishedAt;
		task.Status = model.Status;
		task.RegionId = model.RegionId;
		task.CityId = model.CityId;

		var existing = await _db.TaskTrustedAdmins.Where(x => x.TaskId == task.Id).ToListAsync(cancellationToken);
		_db.TaskTrustedAdmins.RemoveRange(existing);
		foreach(var adminId in trustedAdminIds)
		{
			_db.TaskTrustedAdmins.Add(new TaskTrustedAdmin
			{
				TaskId = task.Id,
				AdminUserId = adminId,
			});
		}

		await _db.SaveChangesAsync(cancellationToken);

		if(coverChanged && previousCoverId.HasValue && previousCoverId != task.CoverImageId)
		{
			await ImageGcHelpers.DeleteOrphanManyAsync(
				_db,
				new[] { previousCoverId.Value },
				cancellationToken);
		}

		return TaskOperationResult.Success();
	}

	public async Task<TaskOperationResult> DeleteAsync(Guid actorUserId, string actorUserPassword, Guid taskId, CancellationToken cancellationToken)
	{
		var actor = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == actorUserId, cancellationToken);

		if(actor is null || !_passwordHasher.Verify(actor.PasswordHash, actorUserPassword))
		{
			_logger.LogWarning("DeleteTask rejected: invalid credentials. ActorUserId={ActorUserId}, TaskId={TaskId}.", actorUserId, taskId);
			return TaskOperationResult.Fail(TaskOperationError.InvalidCredentials);
		}

		var task = await _db.Tasks.FirstOrDefaultAsync(x => x.Id == taskId, cancellationToken);
		if(task is null)
		{
			return TaskOperationResult.Fail(TaskOperationError.TaskNotFound);
		}

		if(task.AuthorUserId != actorUserId)
		{
			_logger.LogWarning("DeleteTask rejected: actor is not author. ActorUserId={ActorUserId}, TaskId={TaskId}, AuthorUserId={AuthorUserId}.", actorUserId, taskId, task.AuthorUserId);
			return TaskOperationResult.Fail(TaskOperationError.Forbidden);
		}

		_db.Tasks.Remove(task);
		await _db.SaveChangesAsync(cancellationToken);
		return TaskOperationResult.Success();
	}

	public async Task<TaskOperationResult> CloseAsync(Guid actorUserId, string actorUserPassword, Guid taskId, CancellationToken cancellationToken)
	{
		var actor = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == actorUserId, cancellationToken);

		if(actor is null || !_passwordHasher.Verify(actor.PasswordHash, actorUserPassword))
		{
			_logger.LogWarning("CloseTask rejected: invalid credentials. ActorUserId={ActorUserId}, TaskId={TaskId}.", actorUserId, taskId);
			return TaskOperationResult.Fail(TaskOperationError.InvalidCredentials);
		}

		var task = await _db.Tasks.FirstOrDefaultAsync(x => x.Id == taskId, cancellationToken);
		if(task is null)
		{
			return TaskOperationResult.Fail(TaskOperationError.TaskNotFound);
		}

		if(task.AuthorUserId != actorUserId)
		{
			_logger.LogWarning("CloseTask rejected: actor is not author. ActorUserId={ActorUserId}, TaskId={TaskId}, AuthorUserId={AuthorUserId}.", actorUserId, taskId, task.AuthorUserId);
			return TaskOperationResult.Fail(TaskOperationError.Forbidden);
		}

		task.Status = TaskStatus.Closed;
		await _db.SaveChangesAsync(cancellationToken);
		return TaskOperationResult.Success();
	}

	public async Task<TaskOperationResult<TaskModel>> GetAdminAsync(Guid actorUserId, string actorUserPassword, Guid taskId, CancellationToken cancellationToken)
	{
		var actor = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == actorUserId, cancellationToken);
		if(actor is null || !_passwordHasher.Verify(actor.PasswordHash, actorUserPassword))
		{
			_logger.LogWarning("GetTaskAdmin rejected: invalid credentials. ActorUserId={ActorUserId}, TaskId={TaskId}.", actorUserId, taskId);
			return TaskOperationResult<TaskModel>.Fail(TaskOperationError.InvalidCredentials);
		}

		if(!actor.IsAdmin)
		{
			_logger.LogWarning("GetTaskAdmin rejected: actor is not admin. ActorUserId={ActorUserId}, TaskId={TaskId}.", actorUserId, taskId);
			return TaskOperationResult<TaskModel>.Fail(TaskOperationError.Forbidden);
		}

		var task = await _db.Tasks.AsNoTracking().FirstOrDefaultAsync(x => x.Id == taskId, cancellationToken);

		if(task is null)
		{
			return TaskOperationResult<TaskModel>.Fail(TaskOperationError.TaskNotFound);
		}

		var trusted = await _db.TaskTrustedAdmins.AsNoTracking()
			.Where(x => x.TaskId == taskId)
			.Select(x => x.AdminUserId)
			.ToListAsync(cancellationToken);

		return TaskOperationResult<TaskModel>.Success(ToModel(task, trusted));
	}

	public async Task<TaskOperationResult<TaskModel>> GetPublicAsync(Guid taskId, CancellationToken cancellationToken)
	{
		var task = await _db.Tasks.AsNoTracking().FirstOrDefaultAsync(x => x.Id == taskId, cancellationToken);
		if(task is null)
		{
			return TaskOperationResult<TaskModel>.Fail(TaskOperationError.TaskNotFound);
		}

		var trusted = await _db.TaskTrustedAdmins.AsNoTracking()
		   .Where(x => x.TaskId == taskId)
		   .Select(x => x.AdminUserId)
		   .ToListAsync(cancellationToken);

		return TaskOperationResult<TaskModel>.Success(ToModel(task, trusted));
	}

	public async Task<IReadOnlyList<TaskModel>> GetByRegionAndCityAsync(int regionId, int cityId, CancellationToken cancellationToken)
	{
		var tasks = await _db.Tasks.AsNoTracking()
			.Where(x => x.RegionId == regionId && (x.CityId == null || x.CityId == cityId))
			.OrderByDescending(x => x.PublishedAt)
			.ToListAsync(cancellationToken);

		return await LoadTrustedAndMapAsync(tasks, cancellationToken);
	}

	public async Task<IReadOnlyList<TaskModel>> GetByRegionAsync(int regionId, CancellationToken cancellationToken)
	{
		var tasks = await _db.Tasks.AsNoTracking()
			.Where(x => x.RegionId == regionId && x.CityId == null)
			.OrderByDescending(x => x.PublishedAt)
			.ToListAsync(cancellationToken);

		return await LoadTrustedAndMapAsync(tasks, cancellationToken);
	}

	public async Task<IReadOnlyList<TaskModel>> GetByCityAsync(int cityId, CancellationToken cancellationToken)
	{
		var tasks = await _db.Tasks.AsNoTracking()
			.Where(x => x.CityId == cityId)
			.OrderByDescending(x => x.PublishedAt)
			.ToListAsync(cancellationToken);

		return await LoadTrustedAndMapAsync(tasks, cancellationToken);
	}

	public async Task<IReadOnlyList<TaskModel>> GetByAdminAsync(Guid adminUserId, CancellationToken cancellationToken)
	{
		var tasks = await _db.Tasks.AsNoTracking()
			.Where(t =>
				t.AuthorUserId == adminUserId
				|| _db.TaskTrustedAdmins.AsNoTracking().Any(x => x.TaskId == t.Id && x.AdminUserId == adminUserId))
			.OrderByDescending(t => t.PublishedAt)
			.ToListAsync(cancellationToken);

		return await LoadTrustedAndMapAsync(tasks, cancellationToken);
	}

	public async Task<TaskOperationResult<IReadOnlyList<TaskModel>>> GetAvailableForUserAsync(Guid userId, CancellationToken cancellationToken)
	{
		var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);

		if(user is null)
		{
			return TaskOperationResult<IReadOnlyList<TaskModel>>.Fail(TaskOperationError.UserNotFound);
		}

		var tasks = await _db.Tasks.AsNoTracking()
		   .Where(t => t.RegionId == user.RegionId && (t.CityId == null || t.CityId == user.CityId))
		   .OrderByDescending(t => t.PublishedAt)
		   .ToListAsync(cancellationToken);

		var mapped = await LoadTrustedAndMapAsync(tasks, cancellationToken);
		return TaskOperationResult<IReadOnlyList<TaskModel>>.Success(mapped);
	}

	public async Task<TaskOperationResult<IReadOnlyList<TaskModel>>> GetByUserSubmittedAsync(Guid actorUserId, string actorUserPassword, CancellationToken cancellationToken)
	{
		var actor = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == actorUserId, cancellationToken);
		if(actor is null || !_passwordHasher.Verify(actor.PasswordHash, actorUserPassword))
		{
			return TaskOperationResult<IReadOnlyList<TaskModel>>.Fail(TaskOperationError.InvalidCredentials);
		}

		var tasks = await _db.TaskSubmissions.AsNoTracking()
			.Where(s => s.UserId == actorUserId && s.ConfirmedAt == null)
			.OrderByDescending(s => s.SubmittedAt)
			.Join(_db.Tasks.AsNoTracking(),
				s => s.TaskId,
				t => t.Id,
				(_, t) => t)
			.ToListAsync(cancellationToken);

		var mapped = await LoadTrustedAndMapAsync(tasks, cancellationToken);
		return TaskOperationResult<IReadOnlyList<TaskModel>>.Success(mapped);
	}

	public async Task<TaskOperationResult<IReadOnlyList<TaskModel>>> GetByUserApprovedAsync(Guid actorUserId, string actorUserPassword, CancellationToken cancellationToken)
	{
		var actor = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == actorUserId, cancellationToken);
		if(actor is null || !_passwordHasher.Verify(actor.PasswordHash, actorUserPassword))
		{
			return TaskOperationResult<IReadOnlyList<TaskModel>>.Fail(TaskOperationError.InvalidCredentials);
		}

		var tasks = await _db.TaskSubmissions.AsNoTracking()
			.Where(s => s.UserId == actorUserId && s.ConfirmedAt != null)
			.OrderByDescending(s => s.ConfirmedAt)
			.Join(_db.Tasks.AsNoTracking(),
				s => s.TaskId,
				t => t.Id,
				(_, t) => t)
			.ToListAsync(cancellationToken);

		var mapped = await LoadTrustedAndMapAsync(tasks, cancellationToken);
		return TaskOperationResult<IReadOnlyList<TaskModel>>.Success(mapped);
	}

	private async Task<TaskOperationError> EnsureRegionCityAsync(int regionId, int? cityId, CancellationToken cancellationToken)
	{
		var regionExists = await _db.Regions.AsNoTracking().AnyAsync(x => x.Id == regionId, cancellationToken);
		if(!regionExists)
		{
			return TaskOperationError.RegionNotFound;
		}

		if(cityId is null)
		{
			return TaskOperationError.None;
		}

		var city = await _db.Cities.AsNoTracking().FirstOrDefaultAsync(x => x.Id == cityId.Value, cancellationToken);
		if(city is null || city.RegionId != regionId)
		{
			return TaskOperationError.CityRegionMismatch;
		}

		return TaskOperationError.None;
	}

	private async Task<IReadOnlyList<TaskModel>> LoadTrustedAndMapAsync(IReadOnlyList<TaskEntity> tasks, CancellationToken cancellationToken)
	{
		if(tasks.Count == 0)
		{
			return Array.Empty<TaskModel>();
		}

		var taskIds = tasks.Select(x => x.Id).ToList();
		var trustedMap = await _db.TaskTrustedAdmins.AsNoTracking()
			.Where(x => taskIds.Contains(x.TaskId))
			.GroupBy(x => x.TaskId)
			.ToDictionaryAsync(
				g => g.Key,
				g => (IReadOnlyList<Guid>)g.Select(x => x.AdminUserId).ToList(),
				cancellationToken);

		return tasks
			.Select(t => ToModel(t, trustedMap.TryGetValue(t.Id, out var ids) ? ids : Array.Empty<Guid>()))
			.ToList();
	}

	private static TaskModel ToModel(TaskEntity t, IReadOnlyList<Guid> trustedAdminIds)
		=> new(
			t.Id,
			t.AuthorUserId,
			t.Title,
			t.Description,
			t.RequirementsText,
			t.RewardPoints,
			t.CoverImageId,
			t.ExecutionLocation,
			t.PublishedAt,
			t.DeadlineAt,
			t.Status,
			t.RegionId,
			t.CityId,
			trustedAdminIds);
}