using LdprActivistDemo.Application.Images;
using LdprActivistDemo.Application.Images.Models;

using Microsoft.EntityFrameworkCore;

namespace LdprActivistDemo.Persistence.Repositories;

public sealed class ImageRepository : IImageRepository
{
	private readonly AppDbContext _db;

	public ImageRepository(AppDbContext db)
	{
		_db = db ?? throw new ArgumentNullException(nameof(db));
	}

	public async Task<ImagePayload?> GetAsync(Guid id, CancellationToken cancellationToken = default)
	{
		if(id == Guid.Empty)
		{
			return null;
		}

		return await _db.Images
			.AsNoTracking()
			.Where(x => x.Id == id)
			.Select(x => new ImagePayload(x.Id, x.ContentType, x.Data))
			.FirstOrDefaultAsync(cancellationToken);
	}

	public async Task<ImagePayload?> GetSystemByNameAsync(string name, CancellationToken cancellationToken = default)
	{
		name = NormalizeSystemImageName(name);
		if(name.Length == 0)
		{
			return null;
		}

		return await _db.SystemImages
			.AsNoTracking()
			.Where(x => x.Name == name)
			.Select(x => new ImagePayload(x.Image.Id, x.Image.ContentType, x.Image.Data))
			.FirstOrDefaultAsync(cancellationToken);
	}

	public async Task<Guid?> GetOwnerUserIdAsync(Guid id, CancellationToken cancellationToken = default)
	{
		if(id == Guid.Empty)
		{
			return null;
		}

		return await _db.Images
			.AsNoTracking()
			.Where(x => x.Id == id)
			.Select(x => (Guid?)x.OwnerUserId)
			.FirstOrDefaultAsync(cancellationToken);
	}

	public async Task<bool> IsUsedBySystemImageAsync(Guid id, CancellationToken cancellationToken = default)
	{
		if(id == Guid.Empty)
		{
			return false;
		}

		return await _db.SystemImages
			.AsNoTracking()
			.AnyAsync(x => x.ImageId == id, cancellationToken);
	}

	public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
	{
		if(id == Guid.Empty)
		{
			return false;
		}

		var idString = id.ToString("D");

		await _db.Users
			.Where(u =>
				u.AvatarImageUrl != null
				&& (u.AvatarImageUrl == idString || EF.Functions.Like(u.AvatarImageUrl, $"%{idString}%")))
			.ExecuteUpdateAsync(
				setters => setters.SetProperty(u => u.AvatarImageUrl, (string?)null),
				cancellationToken);

		var affected = await _db.Images
			.Where(x => x.Id == id)
			.ExecuteDeleteAsync(cancellationToken);

		return affected > 0;
	}

	public async Task<Guid> CreateAsync(Guid ownerUserId, ImageCreateModel model, CancellationToken cancellationToken = default)
	{
		if(ownerUserId == Guid.Empty)
		{
			throw new ArgumentOutOfRangeException(nameof(ownerUserId));
		}

		if(model is null)
		{
			throw new ArgumentNullException(nameof(model));
		}

		if(model.Data is null || model.Data.Length == 0)
		{
			throw new InvalidOperationException("Image data is empty.");
		}

		var id = Guid.NewGuid();

		_db.Images.Add(new ImageEntity
		{
			Id = id,
			OwnerUserId = ownerUserId,
			ContentType = string.IsNullOrWhiteSpace(model.ContentType)
				? "application/octet-stream"
				: model.ContentType.Trim(),
			Data = model.Data,
		});

		await _db.SaveChangesAsync(cancellationToken);

		return id;
	}

	public async Task<IReadOnlyList<Guid>> CreateManyAsync(
	   Guid ownerUserId,
	   IReadOnlyList<ImageCreateModel> models,
	   CancellationToken cancellationToken = default)
	{
		if(ownerUserId == Guid.Empty)
		{
			throw new ArgumentOutOfRangeException(nameof(ownerUserId));
		}

		if(models is null)
		{
			throw new ArgumentNullException(nameof(models));
		}

		if(models.Count == 0)
		{
			return Array.Empty<Guid>();
		}

		var ids = new Guid[models.Count];

		for(var i = 0; i < models.Count; i++)
		{
			var m = models[i];
			if(m is null)
			{
				throw new InvalidOperationException($"Image model at index {i} is null.");
			}

			if(m.Data is null || m.Data.Length == 0)
			{
				throw new InvalidOperationException($"Image data at index {i} is empty.");
			}

			var id = Guid.NewGuid();
			ids[i] = id;

			_db.Images.Add(new ImageEntity
			{
				Id = id,
				OwnerUserId = ownerUserId,
				ContentType = string.IsNullOrWhiteSpace(m.ContentType)
					? "application/octet-stream"
					: m.ContentType.Trim(),
				Data = m.Data,
			});
		}

		await _db.SaveChangesAsync(cancellationToken);

		return ids;
	}

	public async Task<SystemImageStorageUpsertResult> UpsertSystemImageAsync(
		Guid ownerUserId,
		string name,
		ImageCreateModel model,
		CancellationToken cancellationToken = default)
	{
		if(ownerUserId == Guid.Empty)
		{
			throw new ArgumentOutOfRangeException(nameof(ownerUserId));
		}

		if(model is null)
		{
			throw new ArgumentNullException(nameof(model));
		}

		if(model.Data is null || model.Data.Length == 0)
		{
			throw new InvalidOperationException("Image data is empty.");
		}

		var normalizedName = NormalizeSystemImageName(name);
		if(normalizedName.Length == 0)
		{
			throw new InvalidOperationException("System image name is empty.");
		}

		var newImageId = Guid.NewGuid();
		_db.Images.Add(new ImageEntity
		{
			Id = newImageId,
			OwnerUserId = ownerUserId,
			ContentType = string.IsNullOrWhiteSpace(model.ContentType)
				? "application/octet-stream"
				: model.ContentType.Trim(),
			Data = model.Data,
		});

		var existing = await _db.SystemImages.FirstOrDefaultAsync(x => x.Name == normalizedName, cancellationToken);
		var oldImageId = existing?.ImageId;

		SystemImageEntity entity;
		var isCreated = existing is null;
		if(isCreated)
		{
			entity = new SystemImageEntity
			{
				Id = Guid.NewGuid(),
				ImageId = newImageId,
				Name = normalizedName,
			};

			_db.SystemImages.Add(entity);
		}
		else
		{
			entity = existing!;
			entity.ImageId = newImageId;
		}

		await _db.SaveChangesAsync(cancellationToken);

		if(oldImageId.HasValue && oldImageId.Value != newImageId)
		{
			await ImageGcHelpers.DeleteOrphanManyAsync(_db, new[] { oldImageId.Value }, cancellationToken);
		}

		return new SystemImageStorageUpsertResult(isCreated, new SystemImageModel(entity.Id, newImageId, normalizedName));
	}

	private static string NormalizeSystemImageName(string? value)
		=> (value ?? string.Empty).Trim().ToLowerInvariant();
}