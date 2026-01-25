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

	public Task<IReadOnlyList<CityModel>> GetCitiesByRegionAsync(int regionId, CancellationToken cancellationToken)
		=> _cities.GetByRegionAsync(regionId, cancellationToken);

	public async Task<GeoMutationResult<RegionModel>> CreateRegionAsync(
		RegionCreateModel model,
		CancellationToken cancellationToken)
	{
		var hasAccess = await HasAdminAccessAsync(model.ActorUserId, model.ActorPasswordHash, cancellationToken);
		if(!hasAccess)
		{
			return GeoMutationResult<RegionModel>.Fail(GeoMutationError.Unauthorized);
		}

		var name = (model.Name ?? string.Empty).Trim();
		if(string.IsNullOrWhiteSpace(name))
		{
			return GeoMutationResult<RegionModel>.Fail(GeoMutationError.InvalidName);
		}

		var created = await _regions.CreateAsync(name, cancellationToken);
		if(created is null)
		{
			return GeoMutationResult<RegionModel>.Fail(GeoMutationError.Duplicate);
		}

		return GeoMutationResult<RegionModel>.Ok(created);
	}

	public async Task<GeoMutationResult<CityModel>> CreateCityAsync(
		CityCreateModel model,
		CancellationToken cancellationToken)
	{
		var hasAccess = await HasAdminAccessAsync(model.ActorUserId, model.ActorPasswordHash, cancellationToken);
		if(!hasAccess)
		{
			return GeoMutationResult<CityModel>.Fail(GeoMutationError.Unauthorized);
		}

		var name = (model.Name ?? string.Empty).Trim();
		if(string.IsNullOrWhiteSpace(name))
		{
			return GeoMutationResult<CityModel>.Fail(GeoMutationError.InvalidName);
		}

		var regionExists = await _regions.ExistsAsync(model.RegionId, cancellationToken);
		if(!regionExists)
		{
			return GeoMutationResult<CityModel>.Fail(GeoMutationError.RegionNotFound);
		}

		var created = await _cities.CreateAsync(model.RegionId, name, cancellationToken);
		if(created is null)
		{
			return GeoMutationResult<CityModel>.Fail(GeoMutationError.Duplicate);
		}

		return GeoMutationResult<CityModel>.Ok(created);
	}

	private async Task<bool> HasAdminAccessAsync(Guid actorUserId, string actorPasswordHash, CancellationToken cancellationToken)
	{
		var ok = await _users.ValidatePasswordAsync(actorUserId, actorPasswordHash, cancellationToken);
		if(!ok)
		{
			return false;
		}

		return await _users.IsAdminAsync(actorUserId, cancellationToken);
	}
}