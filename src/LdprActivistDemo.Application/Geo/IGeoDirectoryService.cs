using LdprActivistDemo.Application.Geo.Models;

namespace LdprActivistDemo.Application.Geo;

public interface IGeoDirectoryService
{
	Task<IReadOnlyList<RegionModel>> GetRegionsAsync(CancellationToken cancellationToken);
	Task<IReadOnlyList<CityModel>> GetCitiesByRegionAsync(int regionId, CancellationToken cancellationToken);
}