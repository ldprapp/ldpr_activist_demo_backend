using LdprActivistDemo.Application.Diagnostics;
using LdprActivistDemo.Application.Logging;
using LdprActivistDemo.Application.Push;
using LdprActivistDemo.Application.Users.Models;
using LdprActivistDemo.Contracts.Push;
using LdprActivistDemo.Persistence.Logging;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LdprActivistDemo.Persistence;

public sealed class PushDeviceRepository : IPushDeviceRepository
{
	private const string EventUpsert = "push.device.repository.upsert";
	private const string EventDeactivate = "push.device.repository.deactivate";
	private const string EventDeactivateMany = "push.device.repository.deactivate_many";
	private const string EventGetTargetsByUser = "push.device.repository.get_targets_by_user";
	private const string EventGetTargetsByGeo = "push.device.repository.get_targets_by_geo";

	private readonly AppDbContext _db;
	private readonly ILogger<PushDeviceRepository> _logger;

	public PushDeviceRepository(AppDbContext db, ILogger<PushDeviceRepository> logger)
	{
		_db = db ?? throw new ArgumentNullException(nameof(db));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	public async Task UpsertAsync(
		Guid userId,
		string token,
		string platform,
		DateTimeOffset nowUtc,
		CancellationToken cancellationToken)
	{
		var normalizedToken = NormalizeToken(token);
		var normalizedPlatform = NormalizePlatform(platform);
		var properties = new (string Name, object? Value)[]
		{
			("UserId", userId),
			("Platform", normalizedPlatform),
			("Token", MaskToken(normalizedToken)),
		};

		using var scope = _logger.BeginExecutionScope(
			EventUpsert,
			LogLayers.PersistenceRepository,
			PersistenceLogOperations.PushDevices.Upsert,
			properties);

		_logger.LogStarted(
			EventUpsert,
			LogLayers.PersistenceRepository,
			PersistenceLogOperations.PushDevices.Upsert,
			"Push device upsert started.",
			properties);

		try
		{
			cancellationToken.ThrowIfCancellationRequested();

			var entity = await _db.UserPushDevices
				.FirstOrDefaultAsync(x => x.Token == normalizedToken, cancellationToken);

			var created = entity is null;
			var reactivated = false;
			var reassigned = false;

			if(entity is null)
			{
				entity = new UserPushDevice
				{
					Id = Guid.NewGuid(),
					UserId = userId,
					Token = normalizedToken,
					Platform = normalizedPlatform,
					IsActive = true,
					CreatedAtUtc = nowUtc,
					UpdatedAtUtc = nowUtc,
				};

				_db.UserPushDevices.Add(entity);
			}
			else
			{
				reassigned = entity.UserId != userId;
				reactivated = !entity.IsActive;

				entity.UserId = userId;
				entity.Platform = normalizedPlatform;
				entity.IsActive = true;
				entity.UpdatedAtUtc = nowUtc;
			}

			await _db.SaveChangesAsync(cancellationToken);

			_logger.LogCompleted(
				LogLevel.Information,
				EventUpsert,
				LogLayers.PersistenceRepository,
				PersistenceLogOperations.PushDevices.Upsert,
				"Push device upsert completed.",
				StructuredLog.Combine(
					properties,
					("Created", created),
					("Reactivated", reactivated),
					("Reassigned", reassigned)));
		}
		catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
		{
			_logger.LogAborted(
				LogLevel.Information,
				EventUpsert,
				LogLayers.PersistenceRepository,
				PersistenceLogOperations.PushDevices.Upsert,
				"Push device upsert aborted.",
				properties);
			throw;
		}
		catch(Exception ex)
		{
			_logger.LogFailed(
				LogLevel.Error,
				EventUpsert,
				LogLayers.PersistenceRepository,
				PersistenceLogOperations.PushDevices.Upsert,
				"Push device upsert failed.",
				ex,
				properties);
			throw;
		}
	}

	public async Task DeactivateAsync(
		Guid userId,
		string token,
		DateTimeOffset nowUtc,
		CancellationToken cancellationToken)
	{
		var normalizedToken = NormalizeToken(token);
		var properties = new (string Name, object? Value)[]
		{
			("UserId", userId),
			("Token", MaskToken(normalizedToken)),
		};

		using var scope = _logger.BeginExecutionScope(
			EventDeactivate,
			LogLayers.PersistenceRepository,
			PersistenceLogOperations.PushDevices.Deactivate,
			properties);

		_logger.LogStarted(
			EventDeactivate,
			LogLayers.PersistenceRepository,
			PersistenceLogOperations.PushDevices.Deactivate,
			"Push device deactivate started.",
			properties);

		try
		{
			cancellationToken.ThrowIfCancellationRequested();

			var entity = await _db.UserPushDevices
				.FirstOrDefaultAsync(
					x => x.UserId == userId && x.Token == normalizedToken,
					cancellationToken);

			var found = entity is not null;
			var changed = false;

			if(entity is not null && entity.IsActive)
			{
				entity.IsActive = false;
				entity.UpdatedAtUtc = nowUtc;
				changed = true;
				await _db.SaveChangesAsync(cancellationToken);
			}

			_logger.LogCompleted(
				LogLevel.Information,
				EventDeactivate,
				LogLayers.PersistenceRepository,
				PersistenceLogOperations.PushDevices.Deactivate,
				"Push device deactivate completed.",
				StructuredLog.Combine(
					properties,
					("Found", found),
					("Changed", changed)));
		}
		catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
		{
			_logger.LogAborted(
				LogLevel.Information,
				EventDeactivate,
				LogLayers.PersistenceRepository,
				PersistenceLogOperations.PushDevices.Deactivate,
				"Push device deactivate aborted.",
				properties);
			throw;
		}
		catch(Exception ex)
		{
			_logger.LogFailed(
				LogLevel.Error,
				EventDeactivate,
				LogLayers.PersistenceRepository,
				PersistenceLogOperations.PushDevices.Deactivate,
				"Push device deactivate failed.",
				ex,
				properties);
			throw;
		}
	}

	public async Task DeactivateManyByTokensAsync(
		IReadOnlyCollection<string> tokens,
		DateTimeOffset nowUtc,
		CancellationToken cancellationToken)
	{
		var normalizedTokens = (tokens ?? Array.Empty<string>())
			.Select(NormalizeToken)
			.Where(x => !string.IsNullOrWhiteSpace(x))
			.Distinct(StringComparer.Ordinal)
			.ToArray();

		var properties = new (string Name, object? Value)[]
		{
			("TokenCount", normalizedTokens.Length),
		};

		using var scope = _logger.BeginExecutionScope(
			EventDeactivateMany,
			LogLayers.PersistenceRepository,
			PersistenceLogOperations.PushDevices.DeactivateMany,
			properties);

		_logger.LogStarted(
			EventDeactivateMany,
			LogLayers.PersistenceRepository,
			PersistenceLogOperations.PushDevices.DeactivateMany,
			"Push device bulk deactivate started.",
			properties);

		try
		{
			cancellationToken.ThrowIfCancellationRequested();

			var affected = 0;

			if(normalizedTokens.Length > 0)
			{
				affected = await _db.UserPushDevices
					.Where(x => x.IsActive && normalizedTokens.Contains(x.Token))
					.ExecuteUpdateAsync(
						setters => setters
							.SetProperty(x => x.IsActive, false)
							.SetProperty(x => x.UpdatedAtUtc, nowUtc),
						cancellationToken);
			}

			_logger.LogCompleted(
				LogLevel.Information,
				EventDeactivateMany,
				LogLayers.PersistenceRepository,
				PersistenceLogOperations.PushDevices.DeactivateMany,
				"Push device bulk deactivate completed.",
				StructuredLog.Combine(properties, ("Affected", affected)));
		}
		catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
		{
			_logger.LogAborted(
				LogLevel.Information,
				EventDeactivateMany,
				LogLayers.PersistenceRepository,
				PersistenceLogOperations.PushDevices.DeactivateMany,
				"Push device bulk deactivate aborted.",
				properties);
			throw;
		}
		catch(Exception ex)
		{
			_logger.LogFailed(
				LogLevel.Error,
				EventDeactivateMany,
				LogLayers.PersistenceRepository,
				PersistenceLogOperations.PushDevices.DeactivateMany,
				"Push device bulk deactivate failed.",
				ex,
				properties);
			throw;
		}
	}

	public async Task<IReadOnlyList<PushTargetModel>> GetActiveTargetsByUserIdAsync(
		Guid userId,
		CancellationToken cancellationToken)
	{
		return await ExecuteReadAsync(
			EventGetTargetsByUser,
			PersistenceLogOperations.PushDevices.GetTargetsByUser,
			async () => await _db.UserPushDevices
				.AsNoTracking()
				.Where(x => x.UserId == userId)
				.Where(x => x.IsActive)
				.Where(x => x.User.Role != UserRoles.Banned)
				.OrderBy(x => x.CreatedAtUtc)
				.Select(x => new PushTargetModel(x.UserId, x.Token, x.Platform))
				.ToListAsync(cancellationToken),
			cancellationToken,
			("UserId", userId));
	}

	public async Task<IReadOnlyList<PushTargetModel>> GetActiveTargetsForTaskGeoAsync(
		int regionId,
		int? settlementId,
		CancellationToken cancellationToken)
	{
		return await ExecuteReadAsync(
			EventGetTargetsByGeo,
			PersistenceLogOperations.PushDevices.GetTargetsByGeo,
			async () =>
			{
				var query = _db.UserPushDevices
					.AsNoTracking()
					.Where(x => x.IsActive)
					.Where(x => x.User.Role == UserRoles.Activist)
					.Where(x => x.User.RegionId == regionId);

				if(settlementId.HasValue)
				{
					query = query.Where(x => x.User.SettlementId == settlementId.Value);
				}

				return await query
					.OrderBy(x => x.CreatedAtUtc)
					.Select(x => new PushTargetModel(x.UserId, x.Token, x.Platform))
					.ToListAsync(cancellationToken);
			},
			cancellationToken,
			("RegionId", regionId),
			("SettlementId", settlementId));
	}

	private async Task<IReadOnlyList<PushTargetModel>> ExecuteReadAsync(
		string eventName,
		string operationName,
		Func<Task<IReadOnlyList<PushTargetModel>>> action,
		CancellationToken cancellationToken,
		params (string Name, object? Value)[] properties)
	{
		using var scope = _logger.BeginExecutionScope(
			eventName,
			LogLayers.PersistenceRepository,
			operationName,
			properties);

		_logger.LogStarted(
			eventName,
			LogLayers.PersistenceRepository,
			operationName,
			"Push device repository read operation started.",
			properties);

		try
		{
			var result = await action();

			_logger.LogCompleted(
				LogLevel.Debug,
				eventName,
				LogLayers.PersistenceRepository,
				operationName,
				"Push device repository read operation completed.",
				StructuredLog.Combine(properties, ("Count", result.Count)));

			return result;
		}
		catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
		{
			_logger.LogAborted(
				LogLevel.Information,
				eventName,
				LogLayers.PersistenceRepository,
				operationName,
				"Push device repository read operation aborted.",
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
				"Push device repository read operation failed.",
				ex,
				properties);
			throw;
		}
	}

	private static string NormalizeToken(string? value)
	{
		var normalized = (value ?? string.Empty).Trim();
		if(string.IsNullOrWhiteSpace(normalized))
		{
			throw new ArgumentException("Push token is required.", nameof(value));
		}

		return normalized;
	}

	private static string NormalizePlatform(string? value)
	{
		var token = (value ?? string.Empty).Trim().ToLowerInvariant();

		return token switch
		{
			PushPlatform.Android => PushPlatform.Android,
			PushPlatform.Ios => PushPlatform.Ios,
			PushPlatform.Web => PushPlatform.Web,
			_ => throw new ArgumentException("Push platform is invalid.", nameof(value)),
		};
	}

	private static string MaskToken(string value)
	{
		if(string.IsNullOrWhiteSpace(value))
		{
			return "****";
		}

		if(value.Length <= 8)
		{
			return "****";
		}

		return $"***{value[^8..]}";
	}
}