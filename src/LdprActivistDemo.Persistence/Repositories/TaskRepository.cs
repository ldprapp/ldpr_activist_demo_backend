using LdprActivistDemo.Application.Tasks;
using LdprActivistDemo.Application.Tasks.Models;
using LdprActivistDemo.Application.Users;

using Microsoft.EntityFrameworkCore;

using TaskStatus = LdprActivistDemo.Contracts.Tasks.TaskStatus;

namespace LdprActivistDemo.Persistence;

public sealed class TaskRepository : ITaskRepository
{
	private readonly AppDbContext _db;
	private readonly IPasswordHasher _passwordHasher;

	public TaskRepository(AppDbContext db, IPasswordHasher passwordHasher)
	{
		_db = db;
		_passwordHasher = passwordHasher;
	}

	public async Task<Guid> CreateAsync(TaskCreateModel model, CancellationToken cancellationToken)
	{
		var actor = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == model.ActorUserId, cancellationToken);
		if(actor is null || !actor.IsAdmin || !_passwordHasher.Verify(actor.PasswordHash, model.ActorPasswordHash))
		{
			throw new InvalidOperationException("Actor is not an admin or invalid credentials.");
		}

		await EnsureRegionCityAsync(model.RegionId, model.CityId, cancellationToken);

		if(model.RewardPoints < 0)
		{
			throw new InvalidOperationException("RewardPoints must be non-negative.");
		}

		var entity = new TaskEntity
		{
			Id = Guid.NewGuid(),
			AuthorUserId = model.ActorUserId,
			Title = model.Title,
			Description = model.Description,
			RequirementsText = model.RequirementsText,
			RewardPoints = model.RewardPoints,
			CoverImageUrl = model.CoverImageUrl,
			ExecutionLocation = model.ExecutionLocation,
			PublishedAt = model.PublishedAt,
			DeadlineAt = model.DeadlineAt,
			Status = model.Status,
			RegionId = model.RegionId,
			CityId = model.CityId,
		};

		_db.Tasks.Add(entity);

		foreach(var adminId in model.TrustedAdminIds.Distinct())
		{
			_db.TaskTrustedAdmins.Add(new TaskTrustedAdmin
			{
				TaskId = entity.Id,
				AdminUserId = adminId,
			});
		}

		await _db.SaveChangesAsync(cancellationToken);
		return entity.Id;
	}

	public async Task<bool> UpdateAsync(TaskUpdateModel model, CancellationToken cancellationToken)
	{
		var actor = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == model.ActorUserId, cancellationToken);
		if(actor is null || !_passwordHasher.Verify(actor.PasswordHash, model.ActorPasswordHash))
		{
			return false;
		}

		var task = await _db.Tasks.FirstOrDefaultAsync(x => x.Id == model.TaskId, cancellationToken);
		if(task is null)
		{
			return false;
		}

		if(task.AuthorUserId != model.ActorUserId)
		{
			return false;
		}

		await EnsureRegionCityAsync(model.RegionId, model.CityId, cancellationToken);

		task.Title = model.Title;
		task.Description = model.Description;
		task.RequirementsText = model.RequirementsText;
		task.RewardPoints = model.RewardPoints;
		task.CoverImageUrl = model.CoverImageUrl;
		task.ExecutionLocation = model.ExecutionLocation;
		task.PublishedAt = model.PublishedAt;
		task.DeadlineAt = model.DeadlineAt;
		task.Status = model.Status;
		task.RegionId = model.RegionId;
		task.CityId = model.CityId;

		var existing = await _db.TaskTrustedAdmins.Where(x => x.TaskId == task.Id).ToListAsync(cancellationToken);
		_db.TaskTrustedAdmins.RemoveRange(existing);
		foreach(var adminId in model.TrustedAdminIds.Distinct())
		{
			_db.TaskTrustedAdmins.Add(new TaskTrustedAdmin
			{
				TaskId = task.Id,
				AdminUserId = adminId,
			});
		}

		await _db.SaveChangesAsync(cancellationToken);
		return true;
	}

	public async Task<bool> DeleteAsync(Guid actorUserId, string actorPasswordHash, Guid taskId, CancellationToken cancellationToken)
	{
		var actor = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == actorUserId, cancellationToken);
		if(actor is null || !_passwordHasher.Verify(actor.PasswordHash, actorPasswordHash))
		{
			return false;
		}

		var task = await _db.Tasks.FirstOrDefaultAsync(x => x.Id == taskId, cancellationToken);
		if(task is null)
		{
			return false;
		}

		if(task.AuthorUserId != actorUserId)
		{
			return false;
		}

		_db.Tasks.Remove(task);
		await _db.SaveChangesAsync(cancellationToken);
		return true;
	}

	public async Task<bool> CloseAsync(Guid actorUserId, string actorPasswordHash, Guid taskId, CancellationToken cancellationToken)
	{
		var actor = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == actorUserId, cancellationToken);
		if(actor is null || !_passwordHasher.Verify(actor.PasswordHash, actorPasswordHash))
		{
			return false;
		}

		var task = await _db.Tasks.FirstOrDefaultAsync(x => x.Id == taskId, cancellationToken);
		if(task is null)
		{
			return false;
		}

		if(task.AuthorUserId != actorUserId)
		{
			return false;
		}

		task.Status = TaskStatus.Closed;
		await _db.SaveChangesAsync(cancellationToken);
		return true;
	}

	public async Task<TaskModel?> GetAsync(Guid taskId, CancellationToken cancellationToken)
	{
		var task = await _db.Tasks.AsNoTracking()
			.FirstOrDefaultAsync(x => x.Id == taskId, cancellationToken);

		if(task is null)
		{
			return null;
		}

		var trusted = await _db.TaskTrustedAdmins.AsNoTracking()
			.Where(x => x.TaskId == taskId)
			.Select(x => x.AdminUserId)
			.ToListAsync(cancellationToken);

		return ToModel(task, trusted);
	}

	public async Task<IReadOnlyList<TaskModel>> GetByRegionAndCityAsync(int regionId, int cityId, CancellationToken cancellationToken)
	{
		var tasks = await _db.Tasks.AsNoTracking()
			.Where(x => x.RegionId == regionId && (x.CityId == null || x.CityId == cityId))
			.OrderByDescending(x => x.PublishedAt)
			.ToListAsync(cancellationToken);

		var taskIds = tasks.Select(x => x.Id).ToList();
		var trustedMap = await _db.TaskTrustedAdmins.AsNoTracking()
			.Where(x => taskIds.Contains(x.TaskId))
			.GroupBy(x => x.TaskId)
			.ToDictionaryAsync(g => g.Key, g => (IReadOnlyList<Guid>)g.Select(x => x.AdminUserId).ToList(), cancellationToken);

		return tasks
			.Select(t => ToModel(t, trustedMap.TryGetValue(t.Id, out var ids) ? ids : Array.Empty<Guid>()))
			.ToList();
	}

	private async Task EnsureRegionCityAsync(int regionId, int? cityId, CancellationToken cancellationToken)
	{
		var regionExists = await _db.Regions.AsNoTracking().AnyAsync(x => x.Id == regionId, cancellationToken);
		if(!regionExists)
		{
			throw new InvalidOperationException("Region does not exist.");
		}

		if(cityId is null)
		{
			return;
		}

		var city = await _db.Cities.AsNoTracking().FirstOrDefaultAsync(x => x.Id == cityId.Value, cancellationToken);
		if(city is null || city.RegionId != regionId)
		{
			throw new InvalidOperationException("City does not belong to Region or does not exist.");
		}
	}

	private static TaskModel ToModel(TaskEntity t, IReadOnlyList<Guid> trustedAdminIds)
		=> new(
			t.Id,
			t.AuthorUserId,
			t.Title,
			t.Description,
			t.RequirementsText,
			t.RewardPoints,
			t.CoverImageUrl,
			t.ExecutionLocation,
			t.PublishedAt,
			t.DeadlineAt,
			t.Status,
			t.RegionId,
			t.CityId,
			trustedAdminIds);
}