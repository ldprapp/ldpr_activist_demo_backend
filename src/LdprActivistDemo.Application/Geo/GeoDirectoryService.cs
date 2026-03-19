using LdprActivistDemo.Application.Diagnostics;
using LdprActivistDemo.Application.Geo.Models;
using LdprActivistDemo.Application.Logging;
using LdprActivistDemo.Application.Users;
using LdprActivistDemo.Application.Users.Models;

using Microsoft.Extensions.Logging;

namespace LdprActivistDemo.Application.Geo;

public sealed class GeoDirectoryService : IGeoDirectoryService
{
	private readonly IRegionRepository _regions;
	private readonly ISettlementRepository _settlements;
	private readonly IActorAccessService _actorAccess;
	private readonly ILogger<GeoDirectoryService> _logger;

	public GeoDirectoryService(
		IRegionRepository regions,
		ISettlementRepository settlements,
		IActorAccessService actorAccess,
		ILogger<GeoDirectoryService> logger)
	{
		_regions = regions ?? throw new ArgumentNullException(nameof(regions));
		_settlements = settlements ?? throw new ArgumentNullException(nameof(settlements));
		_actorAccess = actorAccess ?? throw new ArgumentNullException(nameof(actorAccess));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	public async Task<IReadOnlyList<RegionModel>> GetRegionsAsync(CancellationToken cancellationToken)
	{
		return await ExecuteReadAsync(
			DomainLogEvents.Geo.Service.GetRegions,
			ApplicationLogOperations.Geo.GetRegions,
			() => _regions.GetAllAsync(cancellationToken),
			cancellationToken);
	}

	public async Task<IReadOnlyList<SettlementModel>> GetSettlementsByRegionAsync(string regionName, CancellationToken cancellationToken)
	{
		return await ExecuteReadAsync(
			DomainLogEvents.Geo.Service.GetSettlementsByRegion,
			ApplicationLogOperations.Geo.GetSettlementsByRegion,
			() => _settlements.GetByRegionAsync(regionName, cancellationToken),
			cancellationToken,
			("RegionName", regionName));
	}

	public async Task<GeoMutationResult<RegionModel>> CreateRegionAsync(
		Guid actorUserId,
		string actorPassword,
		RegionCreateModel model,
		CancellationToken cancellationToken)
	{
		return await ExecuteMutationAsync(
			DomainLogEvents.Geo.Service.CreateRegion,
			ApplicationLogOperations.Geo.CreateRegion,
			async () =>
			{
				var hasAccess = await HasAdminAccessAsync(actorUserId, actorPassword, cancellationToken);
				if(!hasAccess)
				{
					return GeoMutationResult<RegionModel>.Fail(GeoMutationError.Unauthorized);
				}

				var name = NormalizeName(model.Name);
				if(string.IsNullOrWhiteSpace(name))
				{
					return GeoMutationResult<RegionModel>.Fail(GeoMutationError.InvalidName);
				}

				return await _regions.CreateAsync(name, cancellationToken);
			},
			cancellationToken,
			("ActorUserId", actorUserId),
			("RegionName", model.Name));
	}

	public async Task<GeoMutationResult<IReadOnlyList<SettlementModel>>> CreateSettlementsAsync(
		Guid actorUserId,
		string actorPassword,
		string regionName,
		IReadOnlyList<string> settlementNames,
		CancellationToken cancellationToken)
	{
		return await ExecuteMutationAsync(
			DomainLogEvents.Geo.Service.CreateSettlements,
			ApplicationLogOperations.Geo.CreateSettlements,
			async () =>
			{
				var hasAccess = await HasAdminAccessAsync(actorUserId, actorPassword, cancellationToken);
				if(!hasAccess)
				{
					return GeoMutationResult<IReadOnlyList<SettlementModel>>.Fail(GeoMutationError.Unauthorized);
				}

				var normalizedRegionName = NormalizeName(regionName);
				if(string.IsNullOrWhiteSpace(normalizedRegionName))
				{
					return GeoMutationResult<IReadOnlyList<SettlementModel>>.Fail(GeoMutationError.InvalidName);
				}

				if(settlementNames is null || settlementNames.Count == 0)
				{
					return GeoMutationResult<IReadOnlyList<SettlementModel>>.Fail(GeoMutationError.InvalidName);
				}

				var normalizedNames = settlementNames
					.Select(NormalizeName)
					.ToList();

				if(normalizedNames.Any(string.IsNullOrWhiteSpace))
				{
					return GeoMutationResult<IReadOnlyList<SettlementModel>>.Fail(GeoMutationError.InvalidName);
				}

				if(normalizedNames.Count != normalizedNames.Distinct(StringComparer.OrdinalIgnoreCase).Count())
				{
					return GeoMutationResult<IReadOnlyList<SettlementModel>>.Fail(GeoMutationError.Duplicate);
				}

				return await _settlements.CreateManyAsync(normalizedRegionName, normalizedNames, cancellationToken);
			},
			cancellationToken,
			("ActorUserId", actorUserId),
			("RegionName", regionName),
			("RequestedCount", settlementNames?.Count));
	}

	public async Task<GeoMutationResult<RegionModel>> UpdateRegionAsync(
		Guid actorUserId,
		string actorPassword,
		RegionUpdateModel model,
		CancellationToken cancellationToken)
	{
		return await ExecuteMutationAsync(
			DomainLogEvents.Geo.Service.UpdateRegion,
			ApplicationLogOperations.Geo.UpdateRegion,
			async () =>
			{
				var hasAccess = await HasAdminAccessAsync(actorUserId, actorPassword, cancellationToken);
				if(!hasAccess)
				{
					return GeoMutationResult<RegionModel>.Fail(GeoMutationError.Unauthorized);
				}

				var currentName = NormalizeName(model.CurrentName);
				var newName = NormalizeName(model.NewName);
				if(string.IsNullOrWhiteSpace(currentName) || string.IsNullOrWhiteSpace(newName))
				{
					return GeoMutationResult<RegionModel>.Fail(GeoMutationError.InvalidName);
				}

				return await _regions.UpdateAsync(currentName, newName, cancellationToken);
			},
			cancellationToken,
			("ActorUserId", actorUserId),
			("CurrentName", model.CurrentName),
			("NewName", model.NewName));
	}

	public async Task<GeoMutationResult<SettlementModel>> UpdateSettlementAsync(
		Guid actorUserId,
		string actorPassword,
		SettlementUpdateModel model,
		CancellationToken cancellationToken)
	{
		return await ExecuteMutationAsync(
			DomainLogEvents.Geo.Service.UpdateSettlement,
			ApplicationLogOperations.Geo.UpdateSettlement,
			async () =>
			{
				var hasAccess = await HasAdminAccessAsync(actorUserId, actorPassword, cancellationToken);
				if(!hasAccess)
				{
					return GeoMutationResult<SettlementModel>.Fail(GeoMutationError.Unauthorized);
				}

				var regionName = NormalizeName(model.RegionName);
				var currentName = NormalizeName(model.CurrentName);
				var newName = NormalizeName(model.NewName);

				if(string.IsNullOrWhiteSpace(regionName)
				   || string.IsNullOrWhiteSpace(currentName)
				   || string.IsNullOrWhiteSpace(newName))
				{
					return GeoMutationResult<SettlementModel>.Fail(GeoMutationError.InvalidName);
				}

				return await _settlements.UpdateAsync(regionName, currentName, newName, cancellationToken);
			},
			cancellationToken,
			("ActorUserId", actorUserId),
			("RegionName", model.RegionName),
			("CurrentName", model.CurrentName),
			("NewName", model.NewName));
	}

	public async Task<GeoMutationResult> DeleteRegionAsync(
		Guid actorUserId,
		string actorPassword,
		RegionDeleteModel model,
		CancellationToken cancellationToken)
	{
		return await ExecuteMutationAsync(
			DomainLogEvents.Geo.Service.DeleteRegion,
			ApplicationLogOperations.Geo.DeleteRegion,
			async () =>
			{
				var hasAccess = await HasAdminAccessAsync(actorUserId, actorPassword, cancellationToken);
				if(!hasAccess)
				{
					return GeoMutationResult.Fail(GeoMutationError.Unauthorized);
				}

				var regionName = NormalizeName(model.Name);
				if(string.IsNullOrWhiteSpace(regionName))
				{
					return GeoMutationResult.Fail(GeoMutationError.InvalidName);
				}

				var targetRegionName = NormalizeNullableName(model.TargetRegionName);
				return await _regions.DeleteAsync(regionName, targetRegionName, cancellationToken);
			},
			cancellationToken,
			("ActorUserId", actorUserId),
			("RegionName", model.Name),
			("TargetRegionName", model.TargetRegionName));
	}

	public async Task<GeoMutationResult> DeleteSettlementAsync(
		Guid actorUserId,
		string actorPassword,
		SettlementDeleteModel model,
		CancellationToken cancellationToken)
	{
		return await ExecuteMutationAsync(
			DomainLogEvents.Geo.Service.DeleteSettlement,
			ApplicationLogOperations.Geo.DeleteSettlement,
			async () =>
			{
				var hasAccess = await HasAdminAccessAsync(actorUserId, actorPassword, cancellationToken);
				if(!hasAccess)
				{
					return GeoMutationResult.Fail(GeoMutationError.Unauthorized);
				}

				var regionName = NormalizeName(model.RegionName);
				var settlementName = NormalizeName(model.SettlementName);
				if(string.IsNullOrWhiteSpace(regionName) || string.IsNullOrWhiteSpace(settlementName))
				{
					return GeoMutationResult.Fail(GeoMutationError.InvalidName);
				}

				var targetRegionName = NormalizeNullableName(model.TargetRegionName);
				var targetSettlementName = NormalizeNullableName(model.TargetSettlementName);

				return await _settlements.DeleteAsync(
					regionName,
					settlementName,
					targetRegionName,
					targetSettlementName,
					cancellationToken);
			},
			cancellationToken,
			("ActorUserId", actorUserId),
			("RegionName", model.RegionName),
			("SettlementName", model.SettlementName),
			("TargetRegionName", model.TargetRegionName),
			("TargetSettlementName", model.TargetSettlementName));
	}

	public async Task<GeoMutationResult> RestoreRegionAsync(
		Guid actorUserId,
		string actorPassword,
		string regionName,
		CancellationToken cancellationToken)
	{
		return await ExecuteMutationAsync(
			DomainLogEvents.Geo.Service.RestoreRegion,
			ApplicationLogOperations.Geo.RestoreRegion,
			async () =>
			{
				var hasAccess = await HasAdminAccessAsync(actorUserId, actorPassword, cancellationToken);
				if(!hasAccess)
				{
					return GeoMutationResult.Fail(GeoMutationError.Unauthorized);
				}

				var normalizedRegionName = NormalizeName(regionName);
				if(string.IsNullOrWhiteSpace(normalizedRegionName))
				{
					return GeoMutationResult.Fail(GeoMutationError.InvalidName);
				}

				return await _regions.RestoreAsync(normalizedRegionName, cancellationToken);
			},
			cancellationToken,
			("ActorUserId", actorUserId),
			("RegionName", regionName));
	}

	public async Task<GeoMutationResult> RestoreSettlementAsync(
		Guid actorUserId,
		string actorPassword,
		string regionName,
		string settlementName,
		CancellationToken cancellationToken)
	{
		return await ExecuteMutationAsync(
			DomainLogEvents.Geo.Service.RestoreSettlement,
			ApplicationLogOperations.Geo.RestoreSettlement,
			async () =>
			{
				var hasAccess = await HasAdminAccessAsync(actorUserId, actorPassword, cancellationToken);
				if(!hasAccess)
				{
					return GeoMutationResult.Fail(GeoMutationError.Unauthorized);
				}

				var normalizedRegionName = NormalizeName(regionName);
				var normalizedSettlementName = NormalizeName(settlementName);
				if(string.IsNullOrWhiteSpace(normalizedRegionName) || string.IsNullOrWhiteSpace(normalizedSettlementName))
				{
					return GeoMutationResult.Fail(GeoMutationError.InvalidName);
				}

				return await _settlements.RestoreAsync(normalizedRegionName, normalizedSettlementName, cancellationToken);
			},
			cancellationToken,
			("ActorUserId", actorUserId),
			("RegionName", regionName),
			("SettlementName", settlementName));
	}

	private async Task<bool> HasAdminAccessAsync(Guid actorUserId, string actorPassword, CancellationToken cancellationToken)
	{
		var auth = await _actorAccess.AuthenticateAsync(actorUserId, actorPassword, cancellationToken);
		if(!auth.IsSuccess)
		{
			return false;
		}

		return UserRoleRules.IsAdmin(auth.Actor!.Role);
	}

	private async Task<IReadOnlyList<T>> ExecuteReadAsync<T>(
		string eventName,
		string operationName,
		Func<Task<IReadOnlyList<T>>> action,
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
			"Geo application read operation started.",
			properties);

		try
		{
			var result = await action();

			_logger.LogCompleted(
				LogLevel.Debug,
				eventName,
				LogLayers.ApplicationService,
				operationName,
				"Geo application read operation completed.",
				StructuredLog.Combine(
					properties,
					("Count", result.Count)));

			return result;
		}
		catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
		{
			_logger.LogAborted(
				LogLevel.Information,
				eventName,
				LogLayers.ApplicationService,
				operationName,
				"Geo application read operation aborted.",
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
				"Geo application read operation failed.",
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
		using var scope = _logger.BeginExecutionScope(
			eventName,
			LogLayers.ApplicationService,
			operationName,
			properties);

		_logger.LogStarted(
			eventName,
			LogLayers.ApplicationService,
			operationName,
			"Geo application mutation started.",
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
					"Geo application mutation completed.",
					resultProperties);

				return result;
			}

			_logger.LogRejected(
				LogLevel.Warning,
				eventName,
				LogLayers.ApplicationService,
				operationName,
				"Geo application mutation rejected.",
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
				"Geo application mutation aborted.",
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
				"Geo application mutation failed.",
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
		using var scope = _logger.BeginExecutionScope(
			eventName,
			LogLayers.ApplicationService,
			operationName,
			properties);

		_logger.LogStarted(
			eventName,
			LogLayers.ApplicationService,
			operationName,
			"Geo application mutation started.",
			properties);

		try
		{
			var result = await action();
			var resultProperties = new List<(string Name, object? Value)>(properties.Length + 3);
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
					LogLayers.ApplicationService,
					operationName,
					"Geo application mutation completed.",
					resultProperties.ToArray());

				return result;
			}

			_logger.LogRejected(
				LogLevel.Warning,
				eventName,
				LogLayers.ApplicationService,
				operationName,
				"Geo application mutation rejected.",
				resultProperties.ToArray());

			return result;
		}
		catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
		{
			_logger.LogAborted(
				LogLevel.Information,
				eventName,
				LogLayers.ApplicationService,
				operationName,
				"Geo application mutation aborted.",
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
				"Geo application mutation failed.",
				ex,
				properties);
			throw;
		}
	}

	private static string NormalizeName(string? value)
		=> (value ?? string.Empty).Trim();

	private static string? NormalizeNullableName(string? value)
	{
		var normalized = NormalizeName(value);
		return normalized.Length == 0 ? null : normalized;
	}
}