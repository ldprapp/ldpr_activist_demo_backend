using Microsoft.EntityFrameworkCore;

namespace LdprActivistDemo.Persistence.Repositories;

public static class ImageGcHelpers
{
	public static async Task DeleteOrphanManyAsync(AppDbContext db, IReadOnlyCollection<Guid> imageIds, CancellationToken cancellationToken)
	{
		if(imageIds.Count == 0)
		{
			return;
		}

		var ids = imageIds
			.Where(x => x != Guid.Empty)
			.Distinct()
			.ToArray();

		if(ids.Length == 0)
		{
			return;
		}

		var toDelete = new List<Guid>(ids.Length);

		foreach(var id in ids)
		{
			var used = await IsImageUsedAsync(db, id, cancellationToken);
			if(!used)
			{
				toDelete.Add(id);
			}
		}

		if(toDelete.Count == 0)
		{
			return;
		}

		await db.Images
			.Where(i => toDelete.Contains(i.Id))
			.ExecuteDeleteAsync(cancellationToken);
	}

	public static Task DeleteManyAsync(AppDbContext db, IReadOnlyCollection<Guid> imageIds, CancellationToken cancellationToken)
		=> DeleteOrphanManyAsync(db, imageIds, cancellationToken);

	public static bool TryExtractImageId(string? value, out Guid imageId)
	{
		imageId = Guid.Empty;

		value = (value ?? string.Empty).Trim();
		if(value.Length == 0)
		{
			return false;
		}

		if(Guid.TryParse(value, out imageId))
		{
			return true;
		}

		var lastSlash = value.LastIndexOf('/');

		if(lastSlash >= 0 && lastSlash + 1 < value.Length)
		{
			var tail = value[(lastSlash + 1)..].Trim();
			if(Guid.TryParse(tail, out imageId))
			{
				return true;
			}
		}

		imageId = Guid.Empty;
		return false;
	}

	private static async Task<bool> IsImageUsedAsync(AppDbContext db, Guid imageId, CancellationToken cancellationToken)
	{
		var usedByTasks = await db.Tasks
			.AsNoTracking()
			.AnyAsync(t => t.CoverImageId == imageId, cancellationToken);
		if(usedByTasks)
		{
			return true;
		}

		var usedBySubmissions = await db.TaskSubmissionImages
			.AsNoTracking()
			.AnyAsync(x => x.ImageId == imageId, cancellationToken);
		if(usedBySubmissions)
		{
			return true;
		}

		var idString = imageId.ToString("D");
		var usedByUsers = await db.Users
			.AsNoTracking()
			.AnyAsync(u => u.AvatarImageUrl != null && EF.Functions.Like(u.AvatarImageUrl, $"%{idString}%"), cancellationToken);
		return usedByUsers;
	}
}