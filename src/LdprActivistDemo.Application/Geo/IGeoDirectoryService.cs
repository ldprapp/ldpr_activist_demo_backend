using LdprActivistDemo.Application.Geo.Models;

namespace LdprActivistDemo.Application.Geo;

public interface IGeoDirectoryService
{
	Task<IReadOnlyList<RegionModel>> GetRegionsAsync(CancellationToken cancellationToken);
	Task<IReadOnlyList<CityModel>> GetCitiesByRegionAsync(int regionId, CancellationToken cancellationToken);

	Task<GeoMutationResult<RegionModel>> CreateRegionAsync(
		Guid actorUserId,
		string actorPassword,
		RegionCreateModel model,
		CancellationToken cancellationToken);

	Task<GeoMutationResult<CityModel>> CreateCityAsync(
		Guid actorUserId,
		string actorPassword,
		CityCreateModel model,
		CancellationToken cancellationToken);
}