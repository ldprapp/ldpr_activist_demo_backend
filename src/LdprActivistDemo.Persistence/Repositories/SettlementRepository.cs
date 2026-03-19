using LdprActivistDemo.Application.Diagnostics;
using LdprActivistDemo.Application.Geo;
using LdprActivistDemo.Application.Geo.Models;
using LdprActivistDemo.Application.Logging;
using LdprActivistDemo.Persistence.Logging;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Npgsql;

namespace LdprActivistDemo.Persistence;

public sealed class SettlementRepository : ISettlementRepository
{
	private readonly AppDbContext _db;
	private readonly ILogger<SettlementRepository> _logger;

	public SettlementRepository(
		AppDbContext db,
		ILogger<SettlementRepository> logger)
	{
		_db = db ?? throw new ArgumentNullException(nameof(db));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	public async Task<IReadOnlyList<SettlementModel>> GetByRegionAsync(string regionName, CancellationToken cancellationToken)
	{
		return await ExecuteReadAsync(
			DomainLogEvents.Geo.Repository.GetSettlementsByRegion,
			PersistenceLogOperations.Geo.GetSettlementsByRegion,
			async () =>
			{
				var regionKey = NormalizeName(regionName).ToLowerInvariant();
				return await _db.Settlements
					.AsNoTracking()
					.Where(x => x.Region.Name.ToLower() == regionKey)
					.OrderBy(x => x.Name)
					.Select(x => new SettlementModel(x.Name, x.IsDeleted))
					.ToListAsync(cancellationToken);
			},
			cancellationToken,
			result => new (string Name, object? Value)[]
			{
				("Count", result.Count),
			},
			("RegionName", regionName));
	}

	public async Task<int?> GetIdByRegionAndNameAsync(string regionName, string settlementName, CancellationToken cancellationToken)
	{
		return await ExecuteReadAsync(
			DomainLogEvents.Geo.Repository.GetSettlementsByRegion,
			PersistenceLogOperations.Geo.GetSettlementIdByRegionAndName,
			async () =>
			{
				var regionKey = NormalizeName(regionName).ToLowerInvariant();
				var settlementKey = NormalizeName(settlementName).ToLowerInvariant();

				return await _db.Settlements
					.AsNoTracking()
					.Where(x => x.Region.Name.ToLower() == regionKey && x.Name.ToLower() == settlementKey && !x.Region.IsDeleted && !x.IsDeleted)
					.Select(x => (int?)x.Id)
					.FirstOrDefaultAsync(cancellationToken);
			},
			cancellationToken,
			result => new (string Name, object? Value)[]
			{
				("Found", result.HasValue),
				("SettlementId", result),
			},
			("RegionName", regionName),
			("SettlementName", settlementName));
	}

	public async Task<GeoMutationResult<IReadOnlyList<SettlementModel>>> CreateManyAsync(
		 string regionName,
		 IReadOnlyList<string> names,
		 CancellationToken cancellationToken)
	{
		return await ExecuteMutationAsync(
			DomainLogEvents.Geo.Repository.CreateSettlements,
			PersistenceLogOperations.Geo.CreateSettlements,
			async () =>
			{
				var regionKey = NormalizeName(regionName).ToLowerInvariant();

				var region = await _db.Regions
					.AsNoTracking()
					.Where(x => x.Name.ToLower() == regionKey)
					.Select(x => new { x.Id, x.IsDeleted })
					.FirstOrDefaultAsync(cancellationToken);

				if(region is null)
				{
					return GeoMutationResult<IReadOnlyList<SettlementModel>>.Fail(GeoMutationError.RegionNotFound);
				}

				if(region.IsDeleted)
				{
					return GeoMutationResult<IReadOnlyList<SettlementModel>>.Fail(GeoMutationError.ParentRegionDeleted);
				}

				var normalized = names
					.Select(NormalizeName)
					.ToList();

				var keys = normalized
					.Select(x => x.ToLowerInvariant())
					.ToHashSet(StringComparer.Ordinal);

				var exists = await _db.Settlements
					.AsNoTracking()
					.AnyAsync(x => x.RegionId == region.Id && keys.Contains(x.Name.ToLower()), cancellationToken);

				if(exists)
				{
					return GeoMutationResult<IReadOnlyList<SettlementModel>>.Fail(GeoMutationError.Duplicate);
				}

				var entities = normalized
					.Select(name => new Settlement
					{
						RegionId = region.Id,
						Name = name,
					})
					.ToList();

				_db.Settlements.AddRange(entities);

				try
				{
					await _db.SaveChangesAsync(cancellationToken);
				}
				catch(DbUpdateException ex) when(IsUniqueViolation(ex))
				{
					return GeoMutationResult<IReadOnlyList<SettlementModel>>.Fail(GeoMutationError.Duplicate);
				}

				var created = entities
					.Select(x => new SettlementModel(x.Name, x.IsDeleted))
					.ToList();

				return GeoMutationResult<IReadOnlyList<SettlementModel>>.Ok(created);
			},
			cancellationToken,
			("RegionName", regionName),
			("RequestedCount", names?.Count));
	}

	public async Task<GeoMutationResult<SettlementModel>> UpdateAsync(
		 string regionName,
		 string currentName,
		 string newName,
		 CancellationToken cancellationToken)
	{
		return await ExecuteMutationAsync(
			DomainLogEvents.Geo.Repository.UpdateSettlement,
			PersistenceLogOperations.Geo.UpdateSettlement,
			async () =>
			{
				var regionKey = NormalizeName(regionName).ToLowerInvariant();
				var currentKey = NormalizeName(currentName).ToLowerInvariant();
				var newKey = NormalizeName(newName).ToLowerInvariant();

				var region = await _db.Regions
					.AsNoTracking()
					.Where(x => x.Name.ToLower() == regionKey)
					.Select(x => new { x.Id })
					.FirstOrDefaultAsync(cancellationToken);

				if(region is null)
				{
					return GeoMutationResult<SettlementModel>.Fail(GeoMutationError.RegionNotFound);
				}

				var settlement = await _db.Settlements
					.FirstOrDefaultAsync(x => x.RegionId == region.Id && x.Name.ToLower() == currentKey, cancellationToken);

				if(settlement is null)
				{
					return GeoMutationResult<SettlementModel>.Fail(GeoMutationError.SettlementNotFound);
				}

				var duplicate = await _db.Settlements
					.AsNoTracking()
					.AnyAsync(x => x.RegionId == region.Id && x.Id != settlement.Id && x.Name.ToLower() == newKey, cancellationToken);

				if(duplicate)
				{
					return GeoMutationResult<SettlementModel>.Fail(GeoMutationError.Duplicate);
				}

				settlement.Name = NormalizeName(newName);

				try
				{
					await _db.SaveChangesAsync(cancellationToken);
				}
				catch(DbUpdateException ex) when(IsUniqueViolation(ex))
				{
					return GeoMutationResult<SettlementModel>.Fail(GeoMutationError.Duplicate);
				}

				return GeoMutationResult<SettlementModel>.Ok(new SettlementModel(settlement.Name, settlement.IsDeleted));
			},
			cancellationToken,
			("RegionName", regionName),
			("CurrentName", currentName),
			("NewName", newName));
	}

	public async Task<GeoMutationResult> DeleteAsync(
		string regionName,
		string settlementName,
		string? targetRegionName,
		string? targetSettlementName,
		CancellationToken cancellationToken)
	{
		return await ExecuteMutationAsync(
			DomainLogEvents.Geo.Repository.DeleteSettlement,
			PersistenceLogOperations.Geo.DeleteSettlement,
			async () =>
			{
				var regionKey = NormalizeName(regionName).ToLowerInvariant();
				var settlementKey = NormalizeName(settlementName).ToLowerInvariant();

				var region = await _db.Regions
					.Where(x => x.Name.ToLower() == regionKey)
					.FirstOrDefaultAsync(cancellationToken);

				if(region is null)
				{
					return GeoMutationResult.Fail(GeoMutationError.RegionNotFound);
				}

				var settlement = await _db.Settlements
					.FirstOrDefaultAsync(x => x.RegionId == region.Id && x.Name.ToLower() == settlementKey, cancellationToken);

				if(settlement is null)
				{
					return GeoMutationResult.Fail(GeoMutationError.SettlementNotFound);
				}

				var hasTargetRegion = !string.IsNullOrWhiteSpace(targetRegionName);
				var hasTargetSettlement = !string.IsNullOrWhiteSpace(targetSettlementName);

				if(hasTargetRegion != hasTargetSettlement)
				{
					return GeoMutationResult.Fail(GeoMutationError.ValidationFailed);
				}

				Region? targetRegion = null;
				Settlement? targetSettlement = null;

				if(hasTargetRegion)
				{
					var targetRegionKey = NormalizeName(targetRegionName).ToLowerInvariant();
					var targetSettlementKey = NormalizeName(targetSettlementName).ToLowerInvariant();

					if(string.Equals(targetRegionKey, regionKey, StringComparison.Ordinal)
					   && string.Equals(targetSettlementKey, settlementKey, StringComparison.Ordinal))
					{
						return GeoMutationResult.Fail(GeoMutationError.ValidationFailed);
					}

					targetRegion = await _db.Regions
						.FirstOrDefaultAsync(x => x.Name.ToLower() == targetRegionKey && !x.IsDeleted, cancellationToken);

					if(targetRegion is null)
					{
						return GeoMutationResult.Fail(GeoMutationError.RegionNotFound);
					}

					targetSettlement = await _db.Settlements
						.FirstOrDefaultAsync(
							x => x.RegionId == targetRegion.Id
								&& x.Name.ToLower() == targetSettlementKey
								&& !x.IsDeleted,
							cancellationToken);

					if(targetSettlement is null)
					{
						return GeoMutationResult.Fail(GeoMutationError.SettlementNotFound);
					}
				}

				await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

				if(targetRegion is not null && targetSettlement is not null)
				{
					await _db.Users
						.Where(x => x.SettlementId == settlement.Id)
						.ExecuteUpdateAsync(
							setters => setters
								.SetProperty(x => x.RegionId, targetRegion.Id)
								.SetProperty(x => x.SettlementId, targetSettlement.Id),
							cancellationToken);

					await _db.Tasks
						.Where(x => x.SettlementId == settlement.Id)
						.ExecuteUpdateAsync(
							setters => setters
								.SetProperty(x => x.RegionId, targetRegion.Id)
								.SetProperty(x => x.SettlementId, targetSettlement.Id),
							cancellationToken);
				}

				settlement.IsDeleted = true;
				await _db.SaveChangesAsync(cancellationToken);
				await tx.CommitAsync(cancellationToken);

				return GeoMutationResult.Success();
			},
			cancellationToken,
			("RegionName", regionName),
			("SettlementName", settlementName),
			("TargetRegionName", targetRegionName),
			("TargetSettlementName", targetSettlementName));
	}

	public async Task<GeoMutationResult> RestoreAsync(
		string regionName,
		string settlementName,
		CancellationToken cancellationToken)
	{
		return await ExecuteMutationAsync(
			DomainLogEvents.Geo.Repository.RestoreSettlement,
			PersistenceLogOperations.Geo.RestoreSettlement,
			async () =>
			{
				var regionKey = NormalizeName(regionName).ToLowerInvariant();
				var settlementKey = NormalizeName(settlementName).ToLowerInvariant();

				var region = await _db.Regions
					.FirstOrDefaultAsync(x => x.Name.ToLower() == regionKey, cancellationToken);

				if(region is null)
				{
					return GeoMutationResult.Fail(GeoMutationError.RegionNotFound);
				}

				if(region.IsDeleted)
				{
					return GeoMutationResult.Fail(GeoMutationError.ParentRegionDeleted);
				}

				var settlement = await _db.Settlements
					.FirstOrDefaultAsync(x => x.RegionId == region.Id && x.Name.ToLower() == settlementKey, cancellationToken);

				if(settlement is null)
				{
					return GeoMutationResult.Fail(GeoMutationError.SettlementNotFound);
				}

				settlement.IsDeleted = false;
				await _db.SaveChangesAsync(cancellationToken);

				return GeoMutationResult.Success();
			},
			cancellationToken,
			("RegionName", regionName),
			("SettlementName", settlementName));
	}

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
			"Settlement repository read operation started.",
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
				"Settlement repository read operation completed.",
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
				"Settlement repository read operation aborted.",
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
				"Settlement repository read operation failed.",
				ex,
				properties);
			throw;
		}
	}

	private async Task<GeoMutationResult> ExecuteMutationAsync(
		string eventName,
		string operationName,
		Func<Task<GeoMutationResult>> action,
		CancellationToken cancellationToken,
		params (string Name, object? Value)[] properties)
	{
		using var scope = _logger.BeginExecutionScope(eventName, LogLayers.PersistenceRepository, operationName, properties);

		_logger.LogStarted(
			eventName,
			LogLayers.PersistenceRepository,
			operationName,
			"Settlement repository mutation started.",
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
					LogLayers.PersistenceRepository,
					operationName,
					"Settlement repository mutation completed.",
					resultProperties);
			}
			else
			{
				_logger.LogRejected(
					LogLevel.Warning,
					eventName,
					LogLayers.PersistenceRepository,
					operationName,
					"Settlement repository mutation rejected.",
					resultProperties);
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
				"Settlement repository mutation aborted.",
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
				"Settlement repository mutation failed.",
				ex,
				properties);
			throw;
		}
	}

	private async Task<GeoMutationResult<T>> ExecuteMutationAsync<T>(
		string eventName,
		string operationName,
		Func<Task<GeoMutationResult<T>>> action,
		CancellationToken cancellationToken,
		params (string Name, object? Value)[] properties)
	{
		using var scope = _logger.BeginExecutionScope(eventName, LogLayers.PersistenceRepository, operationName, properties);

		_logger.LogStarted(
			eventName,
			LogLayers.PersistenceRepository,
			operationName,
			"Settlement repository mutation started.",
			properties);

		try
		{
			var result = await action();
			var resultProperties = new List<(string Name, object? Value)>(properties.Length + 2);
			resultProperties.AddRange(properties);
			resultProperties.Add(("Error", result.Error));
			resultProperties.Add(("HasValue", result.Value is not null));

			if(result.Value is System.Collections.ICollection collection)
			{
				resultProperties.Add(("Count", collection.Count));
			}

			if(result.IsSuccess)
			{
				_logger.LogCompleted(
					LogLevel.Information,
					eventName,
					LogLayers.PersistenceRepository,
					operationName,
					"Settlement repository mutation completed.",
					resultProperties.ToArray());
			}
			else
			{
				_logger.LogRejected(
					LogLevel.Warning,
					eventName,
					LogLayers.PersistenceRepository,
					operationName,
					"Settlement repository mutation rejected.",
					resultProperties.ToArray());
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
				"Settlement repository mutation aborted.",
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
				"Settlement repository mutation failed.",
				ex,
				properties);
			throw;
		}
	}

	private static bool IsUniqueViolation(DbUpdateException ex)
	{
		return ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
	}

	private static string NormalizeName(string? value)
		=> (value ?? string.Empty).Trim();
}