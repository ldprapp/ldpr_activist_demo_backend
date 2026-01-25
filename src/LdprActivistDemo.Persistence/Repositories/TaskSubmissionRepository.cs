using System.Text.Json;

using LdprActivistDemo.Application.Tasks;
using LdprActivistDemo.Application.Tasks.Models;
using LdprActivistDemo.Application.Users;
using LdprActivistDemo.Application.Users.Models;

using Microsoft.EntityFrameworkCore;

namespace LdprActivistDemo.Persistence;

public sealed class TaskSubmissionRepository : ITaskSubmissionRepository
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.General);

	private readonly AppDbContext _db;
	private readonly IUserRepository _users;
	private readonly IPasswordHasher _passwordHasher;

	public TaskSubmissionRepository(AppDbContext db, IUserRepository users, IPasswordHasher passwordHasher)
	{
		_db = db;
		_users = users;
		_passwordHasher = passwordHasher;
	}

	public async Task<bool> SubmitAsync(TaskSubmissionCreateModel model, CancellationToken cancellationToken)
	{
		var ok = await _users.ValidatePasswordAsync(model.UserId, model.UserPasswordHash, cancellationToken);
		if(!ok)
		{
			return false;
		}

		var taskExists = await _db.Tasks.AsNoTracking().AnyAsync(x => x.Id == model.TaskId, cancellationToken);
		if(!taskExists)
		{
			return false;
		}

		var existing = await _db.TaskSubmissions
			.FirstOrDefaultAsync(x => x.TaskId == model.TaskId && x.UserId == model.UserId, cancellationToken);

		var photosJson = model.PhotoUrls is null ? null : JsonSerializer.Serialize(model.PhotoUrls, JsonOptions);

		if(existing is null)
		{
			_db.TaskSubmissions.Add(new TaskSubmission
			{
				Id = Guid.NewGuid(),
				TaskId = model.TaskId,
				UserId = model.UserId,
				SubmittedAt = model.SubmittedAt,
				PhotosJson = photosJson,
				ProofText = model.ProofText,
			});
		}
		else
		{
			if(existing.ConfirmedAt is not null)
			{
				return false;
			}

			existing.SubmittedAt = model.SubmittedAt;
			existing.PhotosJson = photosJson;
			existing.ProofText = model.ProofText;
		}

		await _db.SaveChangesAsync(cancellationToken);
		return true;
	}

	public async Task<IReadOnlyList<UserFullNameModel>> GetSubmittedUsersAsync(Guid actorUserId, string actorPasswordHash, Guid taskId, CancellationToken cancellationToken)
	{
		var hasAccess = await HasCreatorOrTrustedAccessAsync(actorUserId, actorPasswordHash, taskId, cancellationToken);
		if(!hasAccess)
		{
			return Array.Empty<UserFullNameModel>();
		}

		return await _db.TaskSubmissions.AsNoTracking()
			.Where(x => x.TaskId == taskId && x.ConfirmedAt == null)
			.Join(_db.Users.AsNoTracking(),
				s => s.UserId,
				u => u.Id,
				(_, u) => new UserFullNameModel(u.Id, u.LastName, u.FirstName, u.MiddleName))
			.OrderBy(x => x.LastName).ThenBy(x => x.FirstName).ThenBy(x => x.MiddleName)
			.ToListAsync(cancellationToken);
	}

	public async Task<IReadOnlyList<UserFullNameModel>> GetApprovedUsersAsync(Guid actorUserId, string actorPasswordHash, Guid taskId, CancellationToken cancellationToken)
	{
		var hasAccess = await HasCreatorOrTrustedAccessAsync(actorUserId, actorPasswordHash, taskId, cancellationToken);
		if(!hasAccess)
		{
			return Array.Empty<UserFullNameModel>();
		}

		return await _db.TaskSubmissions.AsNoTracking()
			.Where(x => x.TaskId == taskId && x.ConfirmedAt != null)
			.Join(_db.Users.AsNoTracking(),
				s => s.UserId,
				u => u.Id,
				(_, u) => new UserFullNameModel(u.Id, u.LastName, u.FirstName, u.MiddleName))
			.OrderBy(x => x.LastName).ThenBy(x => x.FirstName).ThenBy(x => x.MiddleName)
			.ToListAsync(cancellationToken);
	}

	public async Task<SubmissionUserViewModel?> GetSubmittedUserAsync(Guid actorUserId, string actorPasswordHash, Guid taskId, Guid userId, CancellationToken cancellationToken)
	{
		var hasAccess = await HasCreatorOrTrustedAccessAsync(actorUserId, actorPasswordHash, taskId, cancellationToken);
		if(!hasAccess)
		{
			return null;
		}

		var user = await _users.GetPublicByIdAsync(userId, cancellationToken);
		if(user is null)
		{
			return null;
		}

		var submission = await _db.TaskSubmissions.AsNoTracking()
			.FirstOrDefaultAsync(x => x.TaskId == taskId && x.UserId == userId, cancellationToken);

		if(submission is null)
		{
			return null;
		}

		return new SubmissionUserViewModel(user, ToModel(submission));
	}

	public async Task<bool> ApproveAsync(Guid actorUserId, string actorPasswordHash, Guid taskId, Guid userId, DateTimeOffset confirmedAt, CancellationToken cancellationToken)
	{
		var hasAccess = await HasCreatorOrTrustedAccessAsync(actorUserId, actorPasswordHash, taskId, cancellationToken);
		if(!hasAccess)
		{
			return false;
		}

		var actor = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == actorUserId, cancellationToken);
		if(actor is null || !actor.IsAdmin || !_passwordHasher.Verify(actor.PasswordHash, actorPasswordHash))
		{
			return false;
		}

		await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

		var submission = await _db.TaskSubmissions
			.FirstOrDefaultAsync(x => x.TaskId == taskId && x.UserId == userId, cancellationToken);
		if(submission is null)
		{
			return false;
		}

		if(submission.ConfirmedAt is not null)
		{
			return true;
		}

		var task = await _db.Tasks.AsNoTracking().FirstOrDefaultAsync(x => x.Id == taskId, cancellationToken);
		if(task is null)
		{
			return false;
		}

		submission.ConfirmedByAdminId = actorUserId;
		submission.ConfirmedAt = confirmedAt;
		await _db.SaveChangesAsync(cancellationToken);

		var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
		if(user is null)
		{
			return false;
		}

		user.Points += task.RewardPoints;
		await _db.SaveChangesAsync(cancellationToken);

		await tx.CommitAsync(cancellationToken);
		return true;
	}

	private async Task<bool> HasCreatorOrTrustedAccessAsync(Guid actorUserId, string actorPasswordHash, Guid taskId, CancellationToken cancellationToken)
	{
		var actor = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == actorUserId, cancellationToken);
		if(actor is null || !_passwordHasher.Verify(actor.PasswordHash, actorPasswordHash))
		{
			return false;
		}

		var task = await _db.Tasks.AsNoTracking().FirstOrDefaultAsync(x => x.Id == taskId, cancellationToken);
		if(task is null)
		{
			return false;
		}

		if(task.AuthorUserId == actorUserId)
		{
			return true;
		}

		return await _db.TaskTrustedAdmins.AsNoTracking()
			.AnyAsync(x => x.TaskId == taskId && x.AdminUserId == actorUserId, cancellationToken);
	}

	private static TaskSubmissionModel ToModel(TaskSubmission s)
	{
		IReadOnlyList<string>? photos = null;
		if(!string.IsNullOrWhiteSpace(s.PhotosJson))
		{
			try
			{
				photos = JsonSerializer.Deserialize<List<string>>(s.PhotosJson, JsonOptions);
			}
			catch
			{
				photos = null;
			}
		}

		return new TaskSubmissionModel(
			s.Id,
			s.TaskId,
			s.UserId,
			s.SubmittedAt,
			s.ConfirmedByAdminId,
			s.ConfirmedAt,
			photos,
			s.ProofText);
	}
}