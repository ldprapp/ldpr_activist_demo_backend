using LdprActivistDemo.Application.Geo.Models;

namespace LdprActivistDemo.Application.Geo;

public sealed class GeoDirectoryService : IGeoDirectoryService
{
	private readonly IRegionRepository _regions;
	private readonly ICityRepository _cities;

	public GeoDirectoryService(IRegionRepository regions, ICityRepository cities)
	{
		_regions = regions;
		_cities = cities;
	}

	public Task<IReadOnlyList<RegionModel>> GetRegionsAsync(CancellationToken cancellationToken)
		=> _regions.GetAllAsync(cancellationToken);

	public Task<IReadOnlyList<CityModel>> GetCitiesByRegionAsync(int regionId, CancellationToken cancellationToken)
		=> _cities.GetByRegionAsync(regionId, cancellationToken);
}