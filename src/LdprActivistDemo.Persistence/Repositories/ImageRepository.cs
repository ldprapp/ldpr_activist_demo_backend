using LdprActivistDemo.Application.Diagnostics;
using LdprActivistDemo.Application.Images;
using LdprActivistDemo.Application.Images.Models;
using LdprActivistDemo.Application.Logging;
using LdprActivistDemo.Persistence.Logging;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LdprActivistDemo.Persistence.Repositories;

public sealed class ImageRepository : IImageRepository
{
	private readonly AppDbContext _db;
	private readonly ILogger<ImageRepository> _logger;

	public ImageRepository(AppDbContext db, ILogger<ImageRepository> logger)
	{
		_db = db ?? throw new ArgumentNullException(nameof(db));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	public async Task<ImagePayload?> GetAsync(Guid id, CancellationToken cancellationToken = default)
	{
		return await ExecuteReadAsync(
			DomainLogEvents.Image.Repository.Get,
			PersistenceLogOperations.Images.Get,
			async () =>
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
			},
			cancellationToken,
			value => new (string Name, object? Value)[]
			{
				("Found", value is not null),
			},
			("ImageId", id));
	}

	public async Task<ImagePayload?> GetSystemByNameAsync(string name, CancellationToken cancellationToken = default)
	{
		return await ExecuteReadAsync(
			DomainLogEvents.Image.Repository.GetSystemByName,
			PersistenceLogOperations.Images.GetSystemByName,
			async () =>
			{
				cancellationToken.ThrowIfCancellationRequested();

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
			},
			cancellationToken,
			value => new (string Name, object? Value)[]
			{
				("Found", value is not null),
			},
			("Name", name));
	}

	public async Task<Guid?> GetOwnerUserIdAsync(Guid id, CancellationToken cancellationToken = default)
	{
		return await ExecuteReadAsync(
			DomainLogEvents.Image.Repository.GetOwnerUserId,
			PersistenceLogOperations.Images.GetOwnerUserId,
			async () =>
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
			},
			cancellationToken,
			value => new (string Name, object? Value)[]
			{
				("Found", value.HasValue),
			},
			("ImageId", id));
	}

	public async Task<bool> IsUsedBySystemImageAsync(Guid id, CancellationToken cancellationToken = default)
	{
		return await ExecuteReadAsync(
			DomainLogEvents.Image.Repository.IsUsedBySystemImage,
			PersistenceLogOperations.Images.IsUsedBySystemImage,
			async () =>
			{
				if(id == Guid.Empty)
				{
					return false;
				}

				return await _db.SystemImages
					.AsNoTracking()
					.AnyAsync(x => x.ImageId == id, cancellationToken);
			},
			cancellationToken,
			value => new (string Name, object? Value)[]
			{
				("IsUsed", value),
			},
			("ImageId", id));
	}

	public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
	{
		return await ExecuteMutationAsync(
			DomainLogEvents.Image.Repository.Delete,
			PersistenceLogOperations.Images.Delete,
			async () =>
			{
				if(id == Guid.Empty)
				{
					return false;
				}

				cancellationToken.ThrowIfCancellationRequested();

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
			},
			cancellationToken,
			value => value,
			value => new (string Name, object? Value)[]
			{
				("Deleted", value),
			},
			("ImageId", id));
	}

	public async Task<Guid> CreateAsync(Guid ownerUserId, ImageCreateModel model, CancellationToken cancellationToken = default)
	{
		return await ExecuteCreateAsync(
			DomainLogEvents.Image.Repository.Create,
			PersistenceLogOperations.Images.Create,
			async () =>
			{
				cancellationToken.ThrowIfCancellationRequested();

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
			},
			cancellationToken,
			id => new (string Name, object? Value)[]
			{
				("ImageId", id),
			},
			("OwnerUserId", ownerUserId));
	}

	public async Task<IReadOnlyList<Guid>> CreateManyAsync(
		Guid ownerUserId,
		IReadOnlyList<ImageCreateModel> models,
		CancellationToken cancellationToken = default)
	{
		return await ExecuteCreateAsync<IReadOnlyList<Guid>>(
			DomainLogEvents.Image.Repository.CreateMany,
			PersistenceLogOperations.Images.CreateMany,
			async () =>
			{
				cancellationToken.ThrowIfCancellationRequested();

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
					return (IReadOnlyList<Guid>)Array.Empty<Guid>();
				}

				var ids = new Guid[models.Count];

				for(var i = 0; i < models.Count; i++)
				{
					cancellationToken.ThrowIfCancellationRequested();

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
			},
			cancellationToken,
			(IReadOnlyList<Guid> ids) => new (string Name, object? Value)[]
			{
				("CreatedCount", ids.Count),
			},
			("OwnerUserId", ownerUserId),
			("RequestedCount", models?.Count));
	}

	public async Task<SystemImageStorageUpsertResult> UpsertSystemImageAsync(
		Guid ownerUserId,
		string name,
		ImageCreateModel model,
		CancellationToken cancellationToken = default)
	{
		return await ExecuteCreateAsync(
			DomainLogEvents.Image.Repository.UpsertSystem,
			PersistenceLogOperations.Images.UpsertSystem,
			async () =>
			{
				cancellationToken.ThrowIfCancellationRequested();

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

				cancellationToken.ThrowIfCancellationRequested();

				if(oldImageId.HasValue && oldImageId.Value != newImageId)
				{
					await ImageGcHelpers.DeleteOrphanManyAsync(_db, new[] { oldImageId.Value }, cancellationToken);
				}

				return new SystemImageStorageUpsertResult(isCreated, new SystemImageModel(entity.Id, newImageId, normalizedName));
			},
			cancellationToken,
			result => new (string Name, object? Value)[]
			{
				("IsCreated", result.IsCreated),
				("SystemImageId", result.Value.Id),
				("ImageId", result.Value.ImageId),
			},
			("OwnerUserId", ownerUserId),
			("Name", name));
	}

	private static string NormalizeSystemImageName(string? value)
		=> (value ?? string.Empty).Trim().ToLowerInvariant();

	private async Task<T> ExecuteReadAsync<T>(
		string eventName,
		string operationName,
		Func<Task<T>> action,
		CancellationToken cancellationToken,
		Func<T, (string Name, object? Value)[]>? resultPropertiesFactory = null,
		params (string Name, object? Value)[] properties)
	{
		using var scope = _logger.BeginExecutionScope(eventName, LogLayers.PersistenceRepository, operationName, properties);

		_logger.LogStarted(
			eventName,
			LogLayers.PersistenceRepository,
			operationName,
			"Image repository read operation started.",
			properties);

		try
		{
			var result = await action();
			var resultProperties = resultPropertiesFactory?.Invoke(result) ?? Array.Empty<(string Name, object? Value)>();

			_logger.LogCompleted(
				LogLevel.Debug,
				eventName,
				LogLayers.PersistenceRepository,
				operationName,
				"Image repository read operation completed.",
				StructuredLog.Combine(properties, resultProperties));

			return result;
		}
		catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
		{
			_logger.LogAborted(
				LogLevel.Information,
				eventName,
				LogLayers.PersistenceRepository,
				operationName,
				"Image repository read operation aborted.",
				properties);
			throw;
		}
		catch(Exception ex)
		{
			_logger.LogFailed(
				LogLevel.Error,
				eventName,
				LogLayers.PersistenceRepository,
				operationName,
				"Image repository read operation failed.",
				ex,
				properties);
			throw;
		}
	}

	private async Task<T> ExecuteCreateAsync<T>(
		string eventName,
		string operationName,
		Func<Task<T>> action,
		CancellationToken cancellationToken,
		Func<T, (string Name, object? Value)[]>? resultPropertiesFactory = null,
		params (string Name, object? Value)[] properties)
	{
		using var scope = _logger.BeginExecutionScope(eventName, LogLayers.PersistenceRepository, operationName, properties);

		_logger.LogStarted(
			eventName,
			LogLayers.PersistenceRepository,
			operationName,
			"Image repository mutation started.",
			properties);

		try
		{
			var result = await action();
			var resultProperties = resultPropertiesFactory?.Invoke(result) ?? Array.Empty<(string Name, object? Value)>();

			_logger.LogCompleted(
				LogLevel.Information,
				eventName,
				LogLayers.PersistenceRepository,
				operationName,
				"Image repository mutation completed.",
				StructuredLog.Combine(properties, resultProperties));

			return result;
		}
		catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
		{
			_logger.LogAborted(
				LogLevel.Information,
				eventName,
				LogLayers.PersistenceRepository,
				operationName,
				"Image repository mutation aborted.",
				properties);
			throw;
		}
		catch(Exception ex)
		{
			_logger.LogFailed(
				LogLevel.Error,
				eventName,
				LogLayers.PersistenceRepository,
				operationName,
				"Image repository mutation failed.",
				ex,
				properties);
			throw;
		}
	}

	private async Task<T> ExecuteMutationAsync<T>(
		string eventName,
		string operationName,
		Func<Task<T>> action,
		CancellationToken cancellationToken,
		Func<T, bool> isSuccess,
		Func<T, (string Name, object? Value)[]>? resultPropertiesFactory = null,
		params (string Name, object? Value)[] properties)
	{
		using var scope = _logger.BeginExecutionScope(eventName, LogLayers.PersistenceRepository, operationName, properties);

		_logger.LogStarted(
			eventName,
			LogLayers.PersistenceRepository,
			operationName,
			"Image repository mutation started.",
			properties);

		try
		{
			var result = await action();
			var resultProperties = resultPropertiesFactory?.Invoke(result) ?? Array.Empty<(string Name, object? Value)>();

			if(isSuccess(result))
			{
				_logger.LogCompleted(
					LogLevel.Information,
					eventName,
					LogLayers.PersistenceRepository,
					operationName,
					"Image repository mutation completed.",
					StructuredLog.Combine(properties, resultProperties));
			}
			else
			{
				_logger.LogRejected(
					LogLevel.Warning,
					eventName,
					LogLayers.PersistenceRepository,
					operationName,
					"Image repository mutation rejected.",
					StructuredLog.Combine(properties, resultProperties));
			}

			return result;
		}
		catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
		{
			_logger.LogAborted(
				LogLevel.Information,
				eventName,
				LogLayers.PersistenceRepository,
				operationName,
				"Image repository mutation aborted.",
				properties);
			throw;
		}
		catch(Exception ex)
		{
			_logger.LogFailed(
				LogLevel.Error,
				eventName,
				LogLayers.PersistenceRepository,
				operationName,
				"Image repository mutation failed.",
				ex,
				properties);
			throw;
		}
	}
}