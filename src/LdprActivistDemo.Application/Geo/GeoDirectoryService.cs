using LdprActivistDemo.Application.Geo.Models;
using LdprActivistDemo.Application.Users;

namespace LdprActivistDemo.Application.Geo;

public sealed class GeoDirectoryService : IGeoDirectoryService
{
	private readonly IRegionRepository _regions;
	private readonly ICityRepository _cities;
	private readonly IUserRepository _users;

	public GeoDirectoryService(IRegionRepository regions, ICityRepository cities, IUserRepository users)
	{
		_regions = regions;
		_cities = cities;
		_users = users;
	}

	public Task<IReadOnlyList<RegionModel>> GetRegionsAsync(CancellationToken cancellationToken)
		=> _regions.GetAllAsync(cancellationToken);

	public Task<IReadOnlyList<CityModel>> GetCitiesByRegionAsync(string regionName, CancellationToken cancellationToken)
		=> _cities.GetByRegionAsync(regionName, cancellationToken);

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

	public async Task<GeoMutationResult<IReadOnlyList<CityModel>>> CreateCitiesAsync(
		Guid actorUserId,
		string actorPassword,
		string regionName,
		IReadOnlyList<string> cityNames,
		CancellationToken cancellationToken)
	{
		var hasAccess = await HasAdminAccessAsync(actorUserId, actorPassword, cancellationToken);
		if(!hasAccess)
		{
			return GeoMutationResult<IReadOnlyList<CityModel>>.Fail(GeoMutationError.Unauthorized);
		}

		regionName = NormalizeName(regionName);
		if(string.IsNullOrWhiteSpace(regionName))
		{
			return GeoMutationResult<IReadOnlyList<CityModel>>.Fail(GeoMutationError.InvalidName);
		}

		if(cityNames is null || cityNames.Count == 0)
		{
			return GeoMutationResult<IReadOnlyList<CityModel>>.Fail(GeoMutationError.InvalidName);
		}

		var normalized = cityNames
			.Select(NormalizeName)
			.ToList();

		if(normalized.Any(string.IsNullOrWhiteSpace))
		{
			return GeoMutationResult<IReadOnlyList<CityModel>>.Fail(GeoMutationError.InvalidName);
		}

		if(normalized.Count != normalized.Distinct(StringComparer.OrdinalIgnoreCase).Count())
		{
			return GeoMutationResult<IReadOnlyList<CityModel>>.Fail(GeoMutationError.Duplicate);
		}

		return await _cities.CreateManyAsync(regionName, normalized, cancellationToken);
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

	public async Task<GeoMutationResult<CityModel>> UpdateCityAsync(
		Guid actorUserId,
		string actorPassword,
		CityUpdateModel model,
		CancellationToken cancellationToken)
	{
		var hasAccess = await HasAdminAccessAsync(actorUserId, actorPassword, cancellationToken);
		if(!hasAccess)
		{
			return GeoMutationResult<CityModel>.Fail(GeoMutationError.Unauthorized);
		}

		var regionName = NormalizeName(model.RegionName);
		var currentName = NormalizeName(model.CurrentName);
		var newName = NormalizeName(model.NewName);

		if(string.IsNullOrWhiteSpace(regionName)
			|| string.IsNullOrWhiteSpace(currentName)
			|| string.IsNullOrWhiteSpace(newName))
		{
			return GeoMutationResult<CityModel>.Fail(GeoMutationError.InvalidName);
		}

		return await _cities.UpdateAsync(regionName, currentName, newName, cancellationToken);
	}

	public async Task<GeoMutationResult> DeleteRegionAsync(
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

		return await _regions.DeleteAsync(regionName, cancellationToken);
	}

	public async Task<GeoMutationResult> DeleteCityAsync(
		Guid actorUserId,
		string actorPassword,
		string regionName,
		string cityName,
		CancellationToken cancellationToken)
	{
		var hasAccess = await HasAdminAccessAsync(actorUserId, actorPassword, cancellationToken);
		if(!hasAccess)
		{
			return GeoMutationResult.Fail(GeoMutationError.Unauthorized);
		}

		regionName = NormalizeName(regionName);
		cityName = NormalizeName(cityName);

		if(string.IsNullOrWhiteSpace(regionName) || string.IsNullOrWhiteSpace(cityName))
		{
			return GeoMutationResult.Fail(GeoMutationError.InvalidName);
		}

		return await _cities.DeleteAsync(regionName, cityName, cancellationToken);
	}

	private async Task<bool> HasAdminAccessAsync(Guid actorUserId, string actorPassword, CancellationToken cancellationToken)
	{
		var ok = await _users.ValidatePasswordAsync(actorUserId, actorPassword, cancellationToken);

		if(!ok)
		{
			return false;
		}

		return await _users.IsAdminAsync(actorUserId, cancellationToken);
	}

	private static string NormalizeName(string? value)
		=> (value ?? string.Empty).Trim();
}