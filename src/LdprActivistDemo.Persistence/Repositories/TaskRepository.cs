using LdprActivistDemo.Application.Tasks;
using LdprActivistDemo.Application.Tasks.Models;
using LdprActivistDemo.Contracts.Tasks;
using LdprActivistDemo.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using TaskStatus = LdprActivistDemo.Contracts.Tasks.TaskStatus;

namespace LdprActivistDemo.Persistence;

public sealed class TaskRepository : ITaskRepository
{
	private readonly AppDbContext _db;
	private readonly ILogger<TaskRepository> _logger;

	public TaskRepository(AppDbContext db, ILogger<TaskRepository> logger)
	{
		_db = db;
		_logger = logger;
	}

	public async Task<TaskOperationResult<Guid>> CreateAsync(Guid actorUserId, TaskCreateModel model, CancellationToken cancellationToken)
	{
		var actor = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == actorUserId, cancellationToken);

		if(actor is null)
		{
			_logger.LogWarning("CreateTask rejected: invalid credentials. ActorUserId={ActorUserId}.", actorUserId);
			return TaskOperationResult<Guid>.Fail(TaskOperationError.InvalidCredentials);
		}

		if(!actor.IsAdmin)
		{
			_logger.LogWarning("CreateTask rejected: actor is not admin. ActorUserId={ActorUserId}.", actorUserId);
			return TaskOperationResult<Guid>.Fail(TaskOperationError.Forbidden);
		}

		var geoResolution = await ResolveRegionCityAsync(model.RegionName, model.CityName, cancellationToken);
		if(geoResolution.Error != TaskOperationError.None)
		{
			_logger.LogWarning("CreateTask rejected: invalid geo. ActorUserId={ActorUserId}, RegionName={RegionName}, CityName={CityName}, Error={Error}.",
				actorUserId, model.RegionName, model.CityName, geoResolution.Error);
			return TaskOperationResult<Guid>.Fail(geoResolution.Error);
		}

		if(model.RewardPoints < 0)
		{
			_logger.LogWarning("CreateTask rejected: negative RewardPoints. ActorUserId={ActorUserId}, RewardPoints={RewardPoints}.",
				actorUserId, model.RewardPoints);
			return TaskOperationResult<Guid>.Fail(TaskOperationError.ValidationFailed);
		}

		if(!TryNormalizeVerificationTypeForCreate(model.VerificationType, out var verificationType))
		{
			_logger.LogWarning("CreateTask rejected: invalid VerificationType. ActorUserId={ActorUserId}, VerificationType={VerificationType}.",
				actorUserId,
				model.VerificationType);
			return TaskOperationResult<Guid>.Fail(TaskOperationError.ValidationFailed);
		}

		if(!TryNormalizeReuseTypeForCreate(model.ReuseType, out var reuseType))
		{
			_logger.LogWarning("CreateTask rejected: invalid ReuseType. ActorUserId={ActorUserId}, ReuseType={ReuseType}.",
				actorUserId,
				model.ReuseType);
			return TaskOperationResult<Guid>.Fail(TaskOperationError.ValidationFailed);
		}

		string? autoVerificationActionType = null;

		if(string.Equals(verificationType, TaskVerificationType.Auto, StringComparison.Ordinal))
		{
			if(string.IsNullOrWhiteSpace(model.AutoVerificationActionType) || string.Equals(model.AutoVerificationActionType, "string", StringComparison.OrdinalIgnoreCase))
			{
				_logger.LogWarning("CreateTask rejected: AutoVerificationActionType is required for auto verification. ActorUserId={ActorUserId}.", actorUserId);
				return TaskOperationResult<Guid>.Fail(TaskOperationError.ValidationFailed);
			}

			if(!TryNormalizeAutoVerificationActionType(model.AutoVerificationActionType, out var normalizedAutoVerificationActionType))
			{
				_logger.LogWarning("CreateTask rejected: invalid AutoVerificationActionType. ActorUserId={ActorUserId}, AutoVerificationActionType={AutoVerificationActionType}.",
					actorUserId, model.AutoVerificationActionType);
				return TaskOperationResult<Guid>.Fail(TaskOperationError.ValidationFailed);
			}

			autoVerificationActionType = normalizedAutoVerificationActionType;
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
			Status = TaskStatus.Open,
			VerificationType = verificationType,
			ReuseType = reuseType,
			AutoVerificationActionType = autoVerificationActionType,
			RegionId = geoResolution.RegionId,
			CityId = geoResolution.CityId,
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

	public async Task<TaskOperationResult> UpdateAsync(Guid actorUserId, Guid taskId, TaskUpdateModel model, CancellationToken cancellationToken)
	{
		var actor = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == actorUserId, cancellationToken);

		if(actor is null)
		{
			_logger.LogWarning("UpdateTask rejected: actor not found. ActorUserId={ActorUserId}, TaskId={TaskId}.", actorUserId, taskId);
			return TaskOperationResult.Fail(TaskOperationError.InvalidCredentials);
		}

		var task = await _db.Tasks.FirstOrDefaultAsync(x => x.Id == taskId, cancellationToken);
		if(task is null)
		{
			return TaskOperationResult.Fail(TaskOperationError.TaskNotFound);
		}

		var previousVerificationType = task.VerificationType;

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

		var geoResolution = await ResolveRegionCityAsync(model.RegionName, model.CityName, cancellationToken);
		if(geoResolution.Error != TaskOperationError.None)
		{
			_logger.LogWarning("UpdateTask rejected: invalid geo. ActorUserId={ActorUserId}, TaskId={TaskId}, RegionName={RegionName}, CityName={CityName}, Error={Error}.",
				actorUserId, taskId, model.RegionName, model.CityName, geoResolution.Error);
			return TaskOperationResult.Fail(geoResolution.Error);
		}

		if(model.RewardPoints < 0)
		{
			_logger.LogWarning("UpdateTask rejected: negative RewardPoints. ActorUserId={ActorUserId}, TaskId={TaskId}, RewardPoints={RewardPoints}.",
				actorUserId, taskId, model.RewardPoints);
			return TaskOperationResult.Fail(TaskOperationError.ValidationFailed);
		}

		if(model.VerificationType is not null)
		{
			if(!TryNormalizeVerificationTypeForUpdate(model.VerificationType, out var verificationType))
			{
				_logger.LogWarning("UpdateTask rejected: invalid VerificationType. ActorUserId={ActorUserId}, TaskId={TaskId}, VerificationType={VerificationType}.",
					actorUserId,
					taskId,
					model.VerificationType);
				return TaskOperationResult.Fail(TaskOperationError.ValidationFailed);
			}

			task.VerificationType = verificationType;
		}

		if(model.ReuseType is not null)
		{
			if(!TryNormalizeReuseTypeForUpdate(model.ReuseType, out var reuseType))
			{
				_logger.LogWarning("UpdateTask rejected: invalid ReuseType. ActorUserId={ActorUserId}, TaskId={TaskId}, ReuseType={ReuseType}.",
					actorUserId,
					taskId,
					model.ReuseType);
				return TaskOperationResult.Fail(TaskOperationError.ValidationFailed);
			}

			task.ReuseType = reuseType;
		}

		var effectiveVerificationType = task.VerificationType;

		if(string.Equals(effectiveVerificationType, TaskVerificationType.Manual, StringComparison.Ordinal))
		{
			task.AutoVerificationActionType = null;
		}
		else
		{
			var switchedToAuto = !string.Equals(previousVerificationType, TaskVerificationType.Auto, StringComparison.Ordinal)
				&& string.Equals(effectiveVerificationType, TaskVerificationType.Auto, StringComparison.Ordinal);

			if(!string.IsNullOrWhiteSpace(model.AutoVerificationActionType) && !string.Equals(model.AutoVerificationActionType, "string", StringComparison.OrdinalIgnoreCase))
			{
				if(!TryNormalizeAutoVerificationActionType(model.AutoVerificationActionType, out var normalizedAutoVerificationActionType))
				{
					_logger.LogWarning("UpdateTask rejected: invalid AutoVerificationActionType. ActorUserId={ActorUserId}, TaskId={TaskId}, AutoVerificationActionType={AutoVerificationActionType}.",
						actorUserId, taskId, model.AutoVerificationActionType);
					return TaskOperationResult.Fail(TaskOperationError.ValidationFailed);
				}

				task.AutoVerificationActionType = normalizedAutoVerificationActionType;
			}
			else if(switchedToAuto)
			{
				_logger.LogWarning("UpdateTask rejected: AutoVerificationActionType is required when switching to auto verification. ActorUserId={ActorUserId}, TaskId={TaskId}.",
					actorUserId, taskId);
				return TaskOperationResult.Fail(TaskOperationError.ValidationFailed);
			}

			if(task.AutoVerificationActionType is null)
			{
				_logger.LogWarning("UpdateTask rejected: AutoVerificationActionType must be set for auto verification. ActorUserId={ActorUserId}, TaskId={TaskId}.",
					actorUserId, taskId);
				return TaskOperationResult.Fail(TaskOperationError.ValidationFailed);
			}
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
		task.DeadlineAt = model.DeadlineAt ?? task.PublishedAt;
		task.RegionId = geoResolution.RegionId;
		task.CityId = geoResolution.CityId;

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

	public async Task<TaskOperationResult> DeleteAsync(Guid actorUserId, Guid taskId, CancellationToken cancellationToken)
	{
		var actorExists = await _db.Users.AsNoTracking()
			.AnyAsync(x => x.Id == actorUserId, cancellationToken);
		if(!actorExists)
		{
			_logger.LogWarning("DeleteTask rejected: actor not found. ActorUserId={ActorUserId}, TaskId={TaskId}.", actorUserId, taskId);
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

	public async Task<TaskOperationResult> OpenAsync(Guid actorUserId, Guid taskId, CancellationToken cancellationToken)
	{
		var actorExists = await _db.Users.AsNoTracking()
			.AnyAsync(x => x.Id == actorUserId, cancellationToken);
		if(!actorExists)
		{
			_logger.LogWarning("OpenTask rejected: actor not found. ActorUserId={ActorUserId}, TaskId={TaskId}.", actorUserId, taskId);
			return TaskOperationResult.Fail(TaskOperationError.InvalidCredentials);
		}

		var task = await _db.Tasks.FirstOrDefaultAsync(x => x.Id == taskId, cancellationToken);
		if(task is null)
		{
			return TaskOperationResult.Fail(TaskOperationError.TaskNotFound);
		}

		if(task.AuthorUserId != actorUserId)
		{
			_logger.LogWarning("OpenTask rejected: actor is not author. ActorUserId={ActorUserId}, TaskId={TaskId}, AuthorUserId={AuthorUserId}.", actorUserId, taskId, task.AuthorUserId);
			return TaskOperationResult.Fail(TaskOperationError.Forbidden);
		}

		if(string.Equals(task.Status, TaskStatus.Open, StringComparison.OrdinalIgnoreCase))
		{
			return TaskOperationResult.Success();
		}

		task.Status = TaskStatus.Open;
		await _db.SaveChangesAsync(cancellationToken);
		return TaskOperationResult.Success();
	}

	public async Task<TaskOperationResult> CloseAsync(Guid actorUserId, Guid taskId, CancellationToken cancellationToken)
	{
		var actorExists = await _db.Users.AsNoTracking()
			.AnyAsync(x => x.Id == actorUserId, cancellationToken);
		if(!actorExists)
		{
			_logger.LogWarning("CloseTask rejected: actor not found. ActorUserId={ActorUserId}, TaskId={TaskId}.", actorUserId, taskId);
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

	public async Task<TaskOperationResult<TaskModel>> GetAdminAsync(Guid actorUserId, Guid taskId, CancellationToken cancellationToken)
	{
		var actor = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == actorUserId, cancellationToken);
		if(actor is null)
		{
			_logger.LogWarning("GetTaskAdmin rejected: actor not found. ActorUserId={ActorUserId}, TaskId={TaskId}.", actorUserId, taskId);
			return TaskOperationResult<TaskModel>.Fail(TaskOperationError.InvalidCredentials);
		}

		if(!actor.IsAdmin)
		{
			_logger.LogWarning("GetTaskAdmin rejected: actor is not admin. ActorUserId={ActorUserId}, TaskId={TaskId}.", actorUserId, taskId);
			return TaskOperationResult<TaskModel>.Fail(TaskOperationError.Forbidden);
		}

		var task = await _db.Tasks.AsNoTracking()
			.Include(x => x.Region)
			.Include(x => x.City)
			.FirstOrDefaultAsync(x => x.Id == taskId, cancellationToken);

		if(task is null)
		{
			return TaskOperationResult<TaskModel>.Fail(TaskOperationError.TaskNotFound);
		}

		var trusted = await _db.TaskTrustedAdmins.AsNoTracking()
			.Where(x => x.TaskId == taskId)
			.Select(x => x.AdminUserId)
			.ToListAsync(cancellationToken);

		return TaskOperationResult<TaskModel>.Success(ToModel(task, trusted, task.Region.Name, task.City?.Name));
	}

	public async Task<TaskOperationResult<TaskModel>> GetPublicAsync(Guid taskId, CancellationToken cancellationToken)
	{
		var task = await _db.Tasks.AsNoTracking()
			.Include(x => x.Region)
			.Include(x => x.City)
			.FirstOrDefaultAsync(x => x.Id == taskId, cancellationToken);
		if(task is null)
		{
			return TaskOperationResult<TaskModel>.Fail(TaskOperationError.TaskNotFound);
		}

		var trusted = await _db.TaskTrustedAdmins.AsNoTracking()
		   .Where(x => x.TaskId == taskId)
		   .Select(x => x.AdminUserId)
		   .ToListAsync(cancellationToken);

		return TaskOperationResult<TaskModel>.Success(ToModel(task, trusted, task.Region.Name, task.City?.Name));
	}

	public async Task<IReadOnlyList<TaskModel>> GetByRegionAndCityAsync(string regionName, string cityName, CancellationToken cancellationToken)
	{
		var regionKey = NormalizeName(regionName).ToLowerInvariant();
		var cityKey = NormalizeName(cityName).ToLowerInvariant();

		var tasks = await _db.Tasks.AsNoTracking()
			.Where(x =>
				x.Region.Name.ToLower() == regionKey
				&& (x.CityId == null || (x.City != null && x.City.Name.ToLower() == cityKey)))
			.OrderByDescending(x => x.PublishedAt)
			.ToListAsync(cancellationToken);

		return await LoadTrustedAndMapAsync(tasks, cancellationToken);
	}

	public async Task<IReadOnlyList<TaskModel>> GetByRegionAsync(string regionName, CancellationToken cancellationToken)
	{
		var regionKey = NormalizeName(regionName).ToLowerInvariant();

		var tasks = await _db.Tasks.AsNoTracking()
			.Where(x => x.Region.Name.ToLower() == regionKey && x.CityId == null)
			.OrderByDescending(x => x.PublishedAt)
			.ToListAsync(cancellationToken);

		return await LoadTrustedAndMapAsync(tasks, cancellationToken);
	}

	public async Task<IReadOnlyList<TaskModel>> GetByCityAsync(string cityName, CancellationToken cancellationToken)
	{
		var cityKey = NormalizeName(cityName).ToLowerInvariant();

		var tasks = await _db.Tasks.AsNoTracking()
			.Where(x => x.City != null && x.City.Name.ToLower() == cityKey)
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

	public async Task<TaskOperationResult<IReadOnlyList<TaskModel>>> GetByUserSubmittedAsync(Guid actorUserId, CancellationToken cancellationToken)
	{
		var tasks = await _db.TaskSubmissions.AsNoTracking()
			.Where(s => s.UserId == actorUserId
				&& (s.DecisionStatus == null
					|| s.DecisionStatus == LdprActivistDemo.Contracts.Tasks.TaskSubmissionDecisionStatus.Rejected))
			.OrderByDescending(s => s.SubmittedAt)
			.Join(_db.Tasks.AsNoTracking(),
				s => s.TaskId,
				t => t.Id,
				(_, t) => t)
			.ToListAsync(cancellationToken);

		var mapped = await LoadTrustedAndMapAsync(tasks, cancellationToken);
		return TaskOperationResult<IReadOnlyList<TaskModel>>.Success(mapped);
	}

	public async Task<TaskOperationResult<IReadOnlyList<TaskModel>>> GetByUserApprovedAsync(Guid actorUserId, CancellationToken cancellationToken)
	{
		var tasks = await _db.TaskSubmissions.AsNoTracking()
			.Where(s => s.UserId == actorUserId
				&& s.DecisionStatus == LdprActivistDemo.Contracts.Tasks.TaskSubmissionDecisionStatus.Approve)
			.OrderByDescending(s => s.DecidedAt)
			.Join(_db.Tasks.AsNoTracking(),
				s => s.TaskId,
				t => t.Id,
				(_, t) => t)
			.ToListAsync(cancellationToken);

		var mapped = await LoadTrustedAndMapAsync(tasks, cancellationToken);
		return TaskOperationResult<IReadOnlyList<TaskModel>>.Success(mapped);
	}

	private async Task<(TaskOperationError Error, int RegionId, int? CityId)> ResolveRegionCityAsync(
		string regionName,
		string? cityName,
		CancellationToken cancellationToken)
	{
		var regionKey = NormalizeName(regionName).ToLowerInvariant();
		if(string.IsNullOrWhiteSpace(regionKey))
		{
			return (TaskOperationError.RegionNotFound, 0, null);
		}

		var region = await _db.Regions.AsNoTracking()
			.Where(x => x.Name.ToLower() == regionKey)
			.Select(x => new { x.Id })
			.FirstOrDefaultAsync(cancellationToken);

		if(region is null)
		{
			return (TaskOperationError.RegionNotFound, 0, null);
		}

		if(string.IsNullOrWhiteSpace(cityName))
		{
			return (TaskOperationError.None, region.Id, null);
		}

		var cityKey = NormalizeName(cityName).ToLowerInvariant();
		var city = await _db.Cities.AsNoTracking()
			.Where(x => x.RegionId == region.Id && x.Name.ToLower() == cityKey)
			.Select(x => new { x.Id })
			.FirstOrDefaultAsync(cancellationToken);

		if(city is null)
		{
			return (TaskOperationError.CityRegionMismatch, 0, null);
		}

		return (TaskOperationError.None, region.Id, city.Id);
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

		var geoMap = await _db.Tasks.AsNoTracking()
			.Where(x => taskIds.Contains(x.Id))
			.Select(x => new
			{
				x.Id,
				RegionName = x.Region.Name,
				CityName = x.City != null ? x.City.Name : null,
			})
			.ToDictionaryAsync(
				x => x.Id,
				x => (x.RegionName, x.CityName),
				cancellationToken);

		return tasks
			.Select(t =>
			{
				var geo = geoMap[t.Id];
				return ToModel(
					t,
					trustedMap.TryGetValue(t.Id, out var ids) ? ids : Array.Empty<Guid>(),
					geo.RegionName,
					geo.CityName);
			})
 			.ToList();
	}

	private static bool TryNormalizeVerificationTypeForCreate(string? raw, out string normalized)
	{
		normalized = TaskVerificationType.Manual;

		if(string.IsNullOrWhiteSpace(raw))
		{
			return true;
		}

		if(string.Equals(raw, "string", StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}

		var token = raw.Trim().ToLowerInvariant();

		if(string.Equals(token, TaskVerificationType.Auto, StringComparison.Ordinal))
		{
			normalized = TaskVerificationType.Auto;
			return true;
		}

		if(string.Equals(token, TaskVerificationType.Manual, StringComparison.Ordinal))
		{
			normalized = TaskVerificationType.Manual;
			return true;
		}

		return false;
	}

	private static bool TryNormalizeVerificationTypeForUpdate(string raw, out string normalized)
	{
		normalized = TaskVerificationType.Manual;

		if(string.IsNullOrWhiteSpace(raw))
		{
			return false;
		}

		if(string.Equals(raw, "string", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		var token = raw.Trim().ToLowerInvariant();

		if(string.Equals(token, TaskVerificationType.Auto, StringComparison.Ordinal))
		{
			normalized = TaskVerificationType.Auto;
			return true;
		}

		if(string.Equals(token, TaskVerificationType.Manual, StringComparison.Ordinal))
		{
			normalized = TaskVerificationType.Manual;
			return true;
		}

		return false;
	}

	private static bool TryNormalizeReuseTypeForCreate(string? raw, out string normalized)
	{
		normalized = TaskReuseType.Disposable;

		if(string.IsNullOrWhiteSpace(raw))
		{
			return true;
		}

		if(string.Equals(raw, "string", StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}

		var token = raw.Trim().ToLowerInvariant();

		if(string.Equals(token, TaskReuseType.Disposable, StringComparison.Ordinal))
		{
			normalized = TaskReuseType.Disposable;
			return true;
		}

		if(string.Equals(token, TaskReuseType.Reusable, StringComparison.Ordinal))
		{
			normalized = TaskReuseType.Reusable;
			return true;
		}

		return false;
	}

	private static bool TryNormalizeReuseTypeForUpdate(string raw, out string normalized)
	{
		normalized = TaskReuseType.Disposable;

		if(string.IsNullOrWhiteSpace(raw))
		{
			return false;
		}

		if(string.Equals(raw, "string", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		var token = raw.Trim().ToLowerInvariant();

		if(string.Equals(token, TaskReuseType.Disposable, StringComparison.Ordinal))
		{
			normalized = TaskReuseType.Disposable;
			return true;
		}

		if(string.Equals(token, TaskReuseType.Reusable, StringComparison.Ordinal))
		{
			normalized = TaskReuseType.Reusable;
			return true;
		}

		return false;
	}

	private static bool TryNormalizeAutoVerificationActionType(string raw, out string normalized)
	{
		normalized = TaskAutoVerificationActionType.InviteFriend;

		if(string.IsNullOrWhiteSpace(raw))
		{
			return false;
		}

		var token = raw.Trim().ToLowerInvariant();

		if(string.Equals(token, TaskAutoVerificationActionType.InviteFriend, StringComparison.Ordinal))
		{
			normalized = TaskAutoVerificationActionType.InviteFriend;
			return true;
		}

		return false;
	}

	private static TaskModel ToModel(TaskEntity t, IReadOnlyList<Guid> trustedAdminIds, string regionName, string? cityName)
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
			regionName,
			cityName,
			trustedAdminIds,
			t.VerificationType,
			t.ReuseType,
			t.AutoVerificationActionType);

	private static string NormalizeName(string? value)
		=> (value ?? string.Empty).Trim();
}