using LdprActivistDemo.Application.Diagnostics;
using LdprActivistDemo.Application.Images.Models;
using LdprActivistDemo.Application.Logging;
using LdprActivistDemo.Application.Users;
using LdprActivistDemo.Application.Users.Models;

using Microsoft.Extensions.Logging;

namespace LdprActivistDemo.Application.Images;

public sealed class ImageService : IImageService
{
	private readonly IImageRepository _images;
	private readonly IActorAccessService _actorAccess;
	private readonly ILogger<ImageService> _logger;

	public ImageService(
		IImageRepository images,
		IActorAccessService actorAccess,
		ILogger<ImageService> logger)
	{
		_images = images ?? throw new ArgumentNullException(nameof(images));
		_actorAccess = actorAccess ?? throw new ArgumentNullException(nameof(actorAccess));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	public async Task<ImagePayload?> GetAsync(Guid id, CancellationToken cancellationToken = default)
	{
		return await ExecuteReadAsync(
			DomainLogEvents.Image.Service.Get,
			ApplicationLogOperations.Images.Get,
			() => _images.GetAsync(id, cancellationToken),
			cancellationToken,
			("ImageId", id));
	}

	public async Task<ImagePayload?> GetSystemByNameAsync(string name, CancellationToken cancellationToken = default)
	{
		return await ExecuteReadAsync(
			DomainLogEvents.Image.Service.GetSystemByName,
			ApplicationLogOperations.Images.GetSystemByName,
			() => _images.GetSystemByNameAsync(name, cancellationToken),
			cancellationToken,
			("Name", name));
	}

	public async Task<ImageDeleteResult> DeleteAsync(
		Guid actorUserId,
		string actorUserPassword,
		Guid id,
		CancellationToken cancellationToken = default)
	{
		return await ExecuteDeleteAsync(
			DomainLogEvents.Image.Service.Delete,
			ApplicationLogOperations.Images.Delete,
			async () =>
			{
				if(actorUserId == Guid.Empty || id == Guid.Empty || string.IsNullOrWhiteSpace(actorUserPassword))
				{
					return ImageDeleteResult.Fail(ImageDeleteError.ValidationFailed);
				}

				var auth = await _actorAccess.AuthenticateAsync(actorUserId, actorUserPassword, cancellationToken);
				if(!auth.IsSuccess)
				{
					return ImageDeleteResult.Fail(ImageDeleteError.InvalidCredentials);
				}

				var ownerUserId = await _images.GetOwnerUserIdAsync(id, cancellationToken);
				if(ownerUserId is null)
				{
					return ImageDeleteResult.Fail(ImageDeleteError.ImageNotFound);
				}

				if(ownerUserId.Value != actorUserId)
				{
					return ImageDeleteResult.Fail(ImageDeleteError.Forbidden);
				}

				if(await _images.IsUsedBySystemImageAsync(id, cancellationToken))
				{
					return ImageDeleteResult.Fail(ImageDeleteError.InUse);
				}

				var deleted = await _images.DeleteAsync(id, cancellationToken);
				return deleted
					? ImageDeleteResult.Success()
					: ImageDeleteResult.Fail(ImageDeleteError.ImageNotFound);
			},
			cancellationToken,
			("ActorUserId", actorUserId),
			("ImageId", id));
	}

	public async Task<Guid> CreateAsync(
		Guid ownerUserId,
		ImageCreateModel model,
		CancellationToken cancellationToken = default)
	{
		return await ExecuteCreateAsync(
			DomainLogEvents.Image.Service.Create,
			ApplicationLogOperations.Images.Create,
			async () =>
			{
				ValidateOwnerUserId(ownerUserId);
				ValidateImageModel(model);
				return await _images.CreateAsync(ownerUserId, model, cancellationToken);
			},
			cancellationToken,
			("OwnerUserId", ownerUserId));
	}

	public async Task<IReadOnlyList<Guid>> CreateManyAsync(
		Guid ownerUserId,
		IReadOnlyList<ImageCreateModel> models,
		CancellationToken cancellationToken = default)
	{
		return await ExecuteCreateManyAsync(
			DomainLogEvents.Image.Service.CreateMany,
			ApplicationLogOperations.Images.CreateMany,
			async () =>
			{
				ValidateOwnerUserId(ownerUserId);
				if(models is null)
				{
					throw new ArgumentNullException(nameof(models));
				}

				if(models.Count == 0)
				{
					return Array.Empty<Guid>();
				}

				for(var i = 0; i < models.Count; i++)
				{
					var current = models[i];
					if(current is null)
					{
						throw new InvalidOperationException($"Image model at index {i} is null.");
					}

					ValidateImageModel(current);
				}

				return await _images.CreateManyAsync(ownerUserId, models, cancellationToken);
			},
			cancellationToken,
			("OwnerUserId", ownerUserId),
			("RequestedCount", models?.Count));
	}

	public async Task<SystemImageUpsertResult> UpsertSystemImageAsync(
		Guid actorUserId,
		string actorUserPassword,
		string name,
		ImageCreateModel model,
		CancellationToken cancellationToken = default)
	{
		return await ExecuteUpsertAsync(
			DomainLogEvents.Image.Service.UpsertSystem,
			ApplicationLogOperations.Images.UpsertSystem,
			async () =>
			{
				var normalizedName = NormalizeSystemImageName(name);
				if(actorUserId == Guid.Empty
				   || string.IsNullOrWhiteSpace(actorUserPassword)
				   || string.IsNullOrWhiteSpace(normalizedName)
				   || model is null
				   || model.Data is null
				   || model.Data.Length == 0)
				{
					return SystemImageUpsertResult.Fail(SystemImageUpsertError.ValidationFailed);
				}

				var auth = await _actorAccess.AuthenticateAsync(actorUserId, actorUserPassword, cancellationToken);
				if(!auth.IsSuccess)
				{
					return SystemImageUpsertResult.Fail(SystemImageUpsertError.InvalidCredentials);
				}

				if(!UserRoleRules.IsAdmin(auth.Actor!.Role))
				{
					return SystemImageUpsertResult.Fail(SystemImageUpsertError.Forbidden);
				}

				var result = await _images.UpsertSystemImageAsync(actorUserId, normalizedName, model, cancellationToken);
				return result.IsCreated
					? SystemImageUpsertResult.Created(result.Value)
					: SystemImageUpsertResult.Updated(result.Value);
			},
			cancellationToken,
			("ActorUserId", actorUserId),
			("Name", name));
	}

	private async Task<ImagePayload?> ExecuteReadAsync(
		string eventName,
		string operationName,
		Func<Task<ImagePayload?>> action,
		CancellationToken cancellationToken,
		params (string Name, object? Value)[] properties)
	{
		using var scope = _logger.BeginExecutionScope(
			eventName,
			LogLayers.ApplicationService,
			operationName,
			properties);

		_logger.LogStarted(
			eventName,
			LogLayers.ApplicationService,
			operationName,
			"Image application read operation started.",
			properties);

		try
		{
			var payload = await action();

			_logger.LogCompleted(
				LogLevel.Debug,
				eventName,
				LogLayers.ApplicationService,
				operationName,
				"Image application read operation completed.",
				StructuredLog.Combine(
					properties,
					("Found", payload is not null)));

			return payload;
		}
		catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
		{
			_logger.LogAborted(
				LogLevel.Information,
				eventName,
				LogLayers.ApplicationService,
				operationName,
				"Image application read operation aborted.",
				properties);
			throw;
		}
		catch(Exception ex)
		{
			_logger.LogFailed(
				LogLevel.Error,
				eventName,
				LogLayers.ApplicationService,
				operationName,
				"Image application read operation failed.",
				ex,
				properties);
			throw;
		}
	}

	private async Task<ImageDeleteResult> ExecuteDeleteAsync(
		string eventName,
		string operationName,
		Func<Task<ImageDeleteResult>> action,
		CancellationToken cancellationToken,
		params (string Name, object? Value)[] properties)
	{
		using var scope = _logger.BeginExecutionScope(eventName, LogLayers.ApplicationService, operationName, properties);

		_logger.LogStarted(
			eventName,
			LogLayers.ApplicationService,
			operationName,
			"Image delete operation started.",
			properties);

		try
		{
			var result = await action();
			var resultProperties = StructuredLog.Combine(properties, ("Error", result.Error));

			if(result.IsSuccess)
			{
				_logger.LogCompleted(
					LogLevel.Information,
					eventName,
					LogLayers.ApplicationService,
					operationName,
					"Image delete operation completed.",
					resultProperties);
				return result;
			}

			_logger.LogRejected(
				LogLevel.Warning,
				eventName,
				LogLayers.ApplicationService,
				operationName,
				"Image delete operation rejected.",
				resultProperties);
			return result;
		}
		catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
		{
			_logger.LogAborted(
				LogLevel.Information,
				eventName,
				LogLayers.ApplicationService,
				operationName,
				"Image delete operation aborted.",
				properties);
			throw;
		}
		catch(Exception ex)
		{
			_logger.LogFailed(
				LogLevel.Error,
				eventName,
				LogLayers.ApplicationService,
				operationName,
				"Image delete operation failed.",
				ex,
				properties);
			throw;
		}
	}

	private async Task<Guid> ExecuteCreateAsync(
		string eventName,
		string operationName,
		Func<Task<Guid>> action,
		CancellationToken cancellationToken,
		params (string Name, object? Value)[] properties)
	{
		using var scope = _logger.BeginExecutionScope(eventName, LogLayers.ApplicationService, operationName, properties);

		_logger.LogStarted(
			eventName,
			LogLayers.ApplicationService,
			operationName,
			"Image create operation started.",
			properties);

		try
		{
			var id = await action();

			_logger.LogCompleted(
				LogLevel.Information,
				eventName,
				LogLayers.ApplicationService,
				operationName,
				"Image create operation completed.",
				StructuredLog.Combine(
					properties,
					("ImageId", id)));

			return id;
		}
		catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
		{
			_logger.LogAborted(
				LogLevel.Information,
				eventName,
				LogLayers.ApplicationService,
				operationName,
				"Image create operation aborted.",
				properties);
			throw;
		}
		catch(Exception ex)
		{
			_logger.LogFailed(
				LogLevel.Error,
				eventName,
				LogLayers.ApplicationService,
				operationName,
				"Image create operation failed.",
				ex,
				properties);
			throw;
		}
	}

	private async Task<IReadOnlyList<Guid>> ExecuteCreateManyAsync(
		string eventName,
		string operationName,
		Func<Task<IReadOnlyList<Guid>>> action,
		CancellationToken cancellationToken,
		params (string Name, object? Value)[] properties)
	{
		using var scope = _logger.BeginExecutionScope(eventName, LogLayers.ApplicationService, operationName, properties);

		_logger.LogStarted(
			eventName,
			LogLayers.ApplicationService,
			operationName,
			"Image bulk create operation started.",
			properties);

		try
		{
			var ids = await action();

			_logger.LogCompleted(
				LogLevel.Information,
				eventName,
				LogLayers.ApplicationService,
				operationName,
				"Image bulk create operation completed.",
				StructuredLog.Combine(
					properties,
					("CreatedCount", ids.Count)));

			return ids;
		}
		catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
		{
			_logger.LogAborted(
				LogLevel.Information,
				eventName,
				LogLayers.ApplicationService,
				operationName,
				"Image bulk create operation aborted.",
				properties);
			throw;
		}
		catch(Exception ex)
		{
			_logger.LogFailed(
				LogLevel.Error,
				eventName,
				LogLayers.ApplicationService,
				operationName,
				"Image bulk create operation failed.",
				ex,
				properties);
			throw;
		}
	}

	private async Task<SystemImageUpsertResult> ExecuteUpsertAsync(
		string eventName,
		string operationName,
		Func<Task<SystemImageUpsertResult>> action,
		CancellationToken cancellationToken,
		params (string Name, object? Value)[] properties)
	{
		using var scope = _logger.BeginExecutionScope(eventName, LogLayers.ApplicationService, operationName, properties);

		_logger.LogStarted(
			eventName,
			LogLayers.ApplicationService,
			operationName,
			"System image upsert operation started.",
			properties);

		try
		{
			var result = await action();
			var resultProperties = StructuredLog.Combine(
				properties,
				("Error", result.Error),
				("IsCreated", result.IsCreated),
				("HasValue", result.Value is not null),
				("ImageId", result.Value?.ImageId),
				("SystemImageId", result.Value?.Id));

			if(result.IsSuccess)
			{
				_logger.LogCompleted(
					LogLevel.Information,
					eventName,
					LogLayers.ApplicationService,
					operationName,
					"System image upsert operation completed.",
					resultProperties);

				return result;
			}

			_logger.LogRejected(
				LogLevel.Warning,
				eventName,
				LogLayers.ApplicationService,
				operationName,
				"System image upsert operation rejected.",
				resultProperties);

			return result;
		}
		catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
		{
			_logger.LogAborted(
				LogLevel.Information,
				eventName,
				LogLayers.ApplicationService,
				operationName,
				"System image upsert operation aborted.",
				properties);
			throw;
		}
		catch(Exception ex)
		{
			_logger.LogFailed(
				LogLevel.Error,
				eventName,
				LogLayers.ApplicationService,
				operationName,
				"System image upsert operation failed.",
				ex,
				properties);
			throw;
		}
	}

	private static void ValidateOwnerUserId(Guid ownerUserId)
	{
		if(ownerUserId == Guid.Empty)
		{
			throw new ArgumentOutOfRangeException(nameof(ownerUserId));
		}
	}

	private static void ValidateImageModel(ImageCreateModel model)
	{
		if(model is null)
		{
			throw new ArgumentNullException(nameof(model));
		}

		if(model.Data is null || model.Data.Length == 0)
		{
			throw new InvalidOperationException("Image data is empty.");
		}
	}

	private static string NormalizeSystemImageName(string? value) =>
		(value ?? string.Empty).Trim().ToLowerInvariant();
}