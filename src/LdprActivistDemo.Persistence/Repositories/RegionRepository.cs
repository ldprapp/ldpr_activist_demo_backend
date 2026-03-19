using LdprActivistDemo.Application.Diagnostics;
using LdprActivistDemo.Application.Geo;
using LdprActivistDemo.Application.Geo.Models;
using LdprActivistDemo.Application.Logging;
using LdprActivistDemo.Persistence.Logging;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Npgsql;

namespace LdprActivistDemo.Persistence;

public sealed class RegionRepository : IRegionRepository
{
	private readonly AppDbContext _db;
	private readonly ILogger<RegionRepository> _logger;

	public RegionRepository(AppDbContext db, ILogger<RegionRepository> logger)
	{
		_db = db ?? throw new ArgumentNullException(nameof(db));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	public async Task<IReadOnlyList<RegionModel>> GetAllAsync(CancellationToken cancellationToken)
	{
		return await ExecuteReadAsync(
			DomainLogEvents.Geo.Repository.GetRegions,
			PersistenceLogOperations.Geo.GetRegions,
			async () => await _db.Regions
				.AsNoTracking()
				.OrderBy(x => x.Name)
				.Select(x => new RegionModel(x.Name, x.IsDeleted))
				.ToListAsync(cancellationToken),
			cancellationToken,
			result => new (string Name, object? Value)[]
			{
				("Count", result.Count),
			});
	}

	public Task<bool> ExistsByNameAsync(string regionName, CancellationToken cancellationToken)
	{
		return ExecuteReadAsync(
			DomainLogEvents.Geo.Repository.GetRegions,
			PersistenceLogOperations.Geo.ExistsRegionByName,
			async () =>
			{
				var key = NormalizeName(regionName).ToLowerInvariant();
				return await _db.Regions
					.AsNoTracking()
					.AnyAsync(x => x.Name.ToLower() == key && !x.IsDeleted, cancellationToken);
			},
			cancellationToken,
			result => new (string Name, object? Value)[]
			{
				("Exists", result),
			},
			("RegionName", regionName));
	}

	public Task<int?> GetIdByNameAsync(string regionName, CancellationToken cancellationToken)
	{
		return ExecuteReadAsync(
			DomainLogEvents.Geo.Repository.GetRegions,
			PersistenceLogOperations.Geo.GetRegionIdByName,
			async () =>
			{
				var key = NormalizeName(regionName).ToLowerInvariant();
				return await _db.Regions
					.AsNoTracking()
					.Where(x => x.Name.ToLower() == key && !x.IsDeleted)
					.Select(x => (int?)x.Id)
					.FirstOrDefaultAsync(cancellationToken);
			},
			cancellationToken,
			result => new (string Name, object? Value)[]
			{
				("Found", result.HasValue),
				("RegionId", result),
			},
			("RegionName", regionName));
	}

	public async Task<GeoMutationResult<RegionModel>> CreateAsync(string name, CancellationToken cancellationToken)
	{
		return await ExecuteMutationAsync(
			DomainLogEvents.Geo.Repository.CreateRegion,
			PersistenceLogOperations.Geo.CreateRegion,
			async () =>
			{
				name = NormalizeName(name);
				var key = name.ToLowerInvariant();

				var exists = await _db.Regions
					.AsNoTracking()
					.AnyAsync(x => x.Name.ToLower() == key, cancellationToken);

				if(exists)
				{
					return GeoMutationResult<RegionModel>.Fail(GeoMutationError.Duplicate);
				}

				var entity = new Region
				{
					Name = name,
				};

				_db.Regions.Add(entity);

				try
				{
					await _db.SaveChangesAsync(cancellationToken);
				}
				catch(DbUpdateException ex) when(IsUniqueViolation(ex))
				{
					return GeoMutationResult<RegionModel>.Fail(GeoMutationError.Duplicate);
				}

				return GeoMutationResult<RegionModel>.Ok(new RegionModel(entity.Name, entity.IsDeleted));
			},
			cancellationToken,
			("RegionName", name));
	}

	public async Task<GeoMutationResult<RegionModel>> UpdateAsync(string currentName, string newName, CancellationToken cancellationToken)
	{
		return await ExecuteMutationAsync(
			DomainLogEvents.Geo.Repository.UpdateRegion,
			PersistenceLogOperations.Geo.UpdateRegion,
			async () =>
			{
				currentName = NormalizeName(currentName);
				newName = NormalizeName(newName);

				var currentKey = currentName.ToLowerInvariant();
				var newKey = newName.ToLowerInvariant();

				var region = await _db.Regions
					.FirstOrDefaultAsync(x => x.Name.ToLower() == currentKey, cancellationToken);

				if(region is null)
				{
					return GeoMutationResult<RegionModel>.Fail(GeoMutationError.RegionNotFound);
				}

				var duplicate = await _db.Regions
					.AsNoTracking()
					.AnyAsync(x => x.Id != region.Id && x.Name.ToLower() == newKey, cancellationToken);

				if(duplicate)
				{
					return GeoMutationResult<RegionModel>.Fail(GeoMutationError.Duplicate);
				}

				region.Name = newName;

				try
				{
					await _db.SaveChangesAsync(cancellationToken);
				}
				catch(DbUpdateException ex) when(IsUniqueViolation(ex))
				{
					return GeoMutationResult<RegionModel>.Fail(GeoMutationError.Duplicate);
				}

				return GeoMutationResult<RegionModel>.Ok(new RegionModel(region.Name, region.IsDeleted));
			},
			cancellationToken,
			("CurrentName", currentName),
			("NewName", newName));
	}

	public async Task<GeoMutationResult> DeleteAsync(string name, string? targetRegionName, CancellationToken cancellationToken)
	{
		return await ExecuteMutationAsync(
			DomainLogEvents.Geo.Repository.DeleteRegion,
			PersistenceLogOperations.Geo.DeleteRegion,
			async () =>
			{
				name = NormalizeName(name);
				var key = name.ToLowerInvariant();

				var region = await _db.Regions
					.Include(x => x.Settlements)
					.FirstOrDefaultAsync(x => x.Name.ToLower() == key, cancellationToken);

				if(region is null)
				{
					return GeoMutationResult.Fail(GeoMutationError.RegionNotFound);
				}

				if(region.Settlements.Any(x => !x.IsDeleted))
				{
					return GeoMutationResult.Fail(GeoMutationError.HasActiveSettlements);
				}

				Region? targetRegion = null;

				if(!string.IsNullOrWhiteSpace(targetRegionName))
				{
					var targetKey = NormalizeName(targetRegionName).ToLowerInvariant();
					if(string.Equals(targetKey, key, StringComparison.Ordinal))
					{
						return GeoMutationResult.Fail(GeoMutationError.ValidationFailed);
					}

					targetRegion = await _db.Regions
						.FirstOrDefaultAsync(x => x.Name.ToLower() == targetKey && !x.IsDeleted, cancellationToken);

					if(targetRegion is null)
					{
						return GeoMutationResult.Fail(GeoMutationError.RegionNotFound);
					}
				}

				await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

				if(targetRegion is not null)
				{
					await _db.Tasks
						.Where(x => x.RegionId == region.Id && x.SettlementId == null)
						.ExecuteUpdateAsync(
							setters => setters.SetProperty(x => x.RegionId, targetRegion.Id),
							cancellationToken);
				}

				region.IsDeleted = true;
				await _db.SaveChangesAsync(cancellationToken);
				await tx.CommitAsync(cancellationToken);

				return GeoMutationResult.Success();
			},
			cancellationToken,
			("RegionName", name),
			("TargetRegionName", targetRegionName));
	}

	public async Task<GeoMutationResult> RestoreAsync(string name, CancellationToken cancellationToken)
	{
		return await ExecuteMutationAsync(
			DomainLogEvents.Geo.Repository.RestoreRegion,
			PersistenceLogOperations.Geo.RestoreRegion,
			async () =>
			{
				name = NormalizeName(name);
				var key = name.ToLowerInvariant();

				var region = await _db.Regions
					.FirstOrDefaultAsync(x => x.Name.ToLower() == key, cancellationToken);

				if(region is null)
				{
					return GeoMutationResult.Fail(GeoMutationError.RegionNotFound);
				}

				region.IsDeleted = false;
				await _db.SaveChangesAsync(cancellationToken);
				return GeoMutationResult.Success();
			},
			cancellationToken,
			("RegionName", name));
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
			"Region repository read operation started.",
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
				"Region repository read operation completed.",
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
				"Region repository read operation aborted.",
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
				"Region repository read operation failed.",
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
			"Region repository mutation started.",
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
					"Region repository mutation completed.",
					resultProperties);
			}
			else
			{
				_logger.LogRejected(
					LogLevel.Warning,
					eventName,
					LogLayers.PersistenceRepository,
					operationName,
					"Region repository mutation rejected.",
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
				"Region repository mutation aborted.",
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
				"Region repository mutation failed.",
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
			"Region repository mutation started.",
			properties);

		try
		{
			var result = await action();
			var resultProperties = new List<(string Name, object? Value)>(properties.Length + 2);
			resultProperties.AddRange(properties);
			resultProperties.Add(("Error", result.Error));
			resultProperties.Add(("HasValue", result.Value is not null));

			if(result.IsSuccess)
			{
				_logger.LogCompleted(
					LogLevel.Information,
					eventName,
					LogLayers.PersistenceRepository,
					operationName,
					"Region repository mutation completed.",
					resultProperties.ToArray());
			}
			else
			{
				_logger.LogRejected(
					LogLevel.Warning,
					eventName,
					LogLayers.PersistenceRepository,
					operationName,
					"Region repository mutation rejected.",
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
				"Region repository mutation aborted.",
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
				"Region repository mutation failed.",
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