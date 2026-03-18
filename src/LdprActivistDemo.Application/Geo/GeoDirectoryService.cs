using LdprActivistDemo.Application.Geo.Models;
using LdprActivistDemo.Application.Users;
using LdprActivistDemo.Application.Users.Models;

namespace LdprActivistDemo.Application.Geo;

public sealed class GeoDirectoryService : IGeoDirectoryService
{
	private readonly IRegionRepository _regions;
	private readonly ISettlementRepository _settlements;
	private readonly IUserRepository _users;

	public GeoDirectoryService(IRegionRepository regions, ISettlementRepository settlements, IUserRepository users)
	{
		_regions = regions;
		_settlements = settlements;
		_users = users;
	}

	public Task<IReadOnlyList<RegionModel>> GetRegionsAsync(CancellationToken cancellationToken)
		=> _regions.GetAllAsync(cancellationToken);

	public Task<IReadOnlyList<SettlementModel>> GetSettlementsByRegionAsync(string regionName, CancellationToken cancellationToken)
		=> _settlements.GetByRegionAsync(regionName, cancellationToken);

	public async Task<GeoMutationResult<RegionModel>> CreateRegionAsync(
		Guid actorUserId,
		string actorPassword,
		RegionCreateModel model,
		CancellationToken cancellationToken)
	{
		var hasAccess = await HasAdminAccessAsync(actorUserId, actorPassword, cancellationToken);
		if(!hasAccess)
		{
			return GeoMutationResult<RegionModel>.Fail(GeoMutationError.Unauthorized);
		}

		var name = (model.Name ?? string.Empty).Trim();
		if(string.IsNullOrWhiteSpace(name))
		{
			return GeoMutationResult<RegionModel>.Fail(GeoMutationError.InvalidName);
		}

		return await _regions.CreateAsync(name, cancellationToken);
	}

	public async Task<GeoMutationResult<IReadOnlyList<SettlementModel>>> CreateSettlementsAsync(
 		Guid actorUserId,
 		string actorPassword,
 		string regionName,
		IReadOnlyList<string> settlementNames,
 		CancellationToken cancellationToken)
	{
		var hasAccess = await HasAdminAccessAsync(actorUserId, actorPassword, cancellationToken);
		if(!hasAccess)
		{
			return GeoMutationResult<IReadOnlyList<SettlementModel>>.Fail(GeoMutationError.Unauthorized);
		}

		regionName = NormalizeName(regionName);
		if(string.IsNullOrWhiteSpace(regionName))
		{
			return GeoMutationResult<IReadOnlyList<SettlementModel>>.Fail(GeoMutationError.InvalidName);
		}

		if(settlementNames is null || settlementNames.Count == 0)
		{
			return GeoMutationResult<IReadOnlyList<SettlementModel>>.Fail(GeoMutationError.InvalidName);
		}

		var normalized = settlementNames
							.Select(NormalizeName)
							.ToList();

		if(normalized.Any(string.IsNullOrWhiteSpace))
		{
			return GeoMutationResult<IReadOnlyList<SettlementModel>>.Fail(GeoMutationError.InvalidName);
		}

		if(normalized.Count != normalized.Distinct(StringComparer.OrdinalIgnoreCase).Count())
		{
			return GeoMutationResult<IReadOnlyList<SettlementModel>>.Fail(GeoMutationError.Duplicate);
		}

		return await _settlements.CreateManyAsync(regionName, normalized, cancellationToken);
	}

	public async Task<GeoMutationResult<RegionModel>> UpdateRegionAsync(
		Guid actorUserId,
		string actorPassword,
		RegionUpdateModel model,
		CancellationToken cancellationToken)
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
	}

	public async Task<GeoMutationResult<SettlementModel>> UpdateSettlementAsync(
 		Guid actorUserId,
 		string actorPassword,
		SettlementUpdateModel model,
 		CancellationToken cancellationToken)
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
	}

	public async Task<GeoMutationResult> DeleteRegionAsync(
		Guid actorUserId,
		string actorPassword,
		RegionDeleteModel model,
		CancellationToken cancellationToken)
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
	}

	public async Task<GeoMutationResult> DeleteSettlementAsync(
 		Guid actorUserId,
 		string actorPassword,
		SettlementDeleteModel model,
 		CancellationToken cancellationToken)
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

		return await _settlements.DeleteAsync(regionName, settlementName, targetRegionName, targetSettlementName, cancellationToken);
	}

	public async Task<GeoMutationResult> RestoreRegionAsync(
		Guid actorUserId,
		string actorPassword,
		string regionName,
		CancellationToken cancellationToken)
	{
		var hasAccess = await HasAdminAccessAsync(actorUserId, actorPassword, cancellationToken);
		if(!hasAccess)
		{
			return GeoMutationResult.Fail(GeoMutationError.Unauthorized);
		}

		regionName = NormalizeName(regionName);
		if(string.IsNullOrWhiteSpace(regionName))
		{
			return GeoMutationResult.Fail(GeoMutationError.InvalidName);
		}

		return await _regions.RestoreAsync(regionName, cancellationToken);
	}

	public async Task<GeoMutationResult> RestoreSettlementAsync(
 		Guid actorUserId,
 		string actorPassword,
 		string regionName,
		string settlementName,
 		CancellationToken cancellationToken)
	{
		var hasAccess = await HasAdminAccessAsync(actorUserId, actorPassword, cancellationToken);
		if(!hasAccess)
		{
			return GeoMutationResult.Fail(GeoMutationError.Unauthorized);
		}

		regionName = NormalizeName(regionName);
		settlementName = NormalizeName(settlementName);

		if(string.IsNullOrWhiteSpace(regionName) || string.IsNullOrWhiteSpace(settlementName))
		{
			return GeoMutationResult.Fail(GeoMutationError.InvalidName);
		}

		return await _settlements.RestoreAsync(regionName, settlementName, cancellationToken);
	}

	private async Task<bool> HasAdminAccessAsync(Guid actorUserId, string actorPassword, CancellationToken cancellationToken)
	{
		var ok = await _users.ValidatePasswordAsync(actorUserId, actorPassword, cancellationToken);
		if(!ok)
		{
			return false;
		}

		var actor = await _users.GetInternalByIdAsync(actorUserId, cancellationToken);
		if(actor is null)
		{
			return false;
		}

		return UserRoleRules.IsAdmin(actor.Role);
	}

	private static string NormalizeName(string? value)
		=> (value ?? string.Empty).Trim();

	private static string? NormalizeNullableName(string? value)
	{
		var normalized = NormalizeName(value);
		return normalized.Length == 0 ? null : normalized;
	}
}