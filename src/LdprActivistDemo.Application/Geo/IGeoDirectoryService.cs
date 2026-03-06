using LdprActivistDemo.Application.Geo.Models;

namespace LdprActivistDemo.Application.Geo;

public interface IGeoDirectoryService
{
	Task<IReadOnlyList<RegionModel>> GetRegionsAsync(CancellationToken cancellationToken);
	Task<IReadOnlyList<CityModel>> GetCitiesByRegionAsync(string regionName, CancellationToken cancellationToken);

	Task<GeoMutationResult<RegionModel>> CreateRegionAsync(
		Guid actorUserId,
		string actorPassword,
		RegionCreateModel model,
		CancellationToken cancellationToken);

	Task<GeoMutationResult<IReadOnlyList<CityModel>>> CreateCitiesAsync(
		Guid actorUserId,
		string actorPassword,
		string regionName,
		IReadOnlyList<string> cityNames,
		CancellationToken cancellationToken);

	Task<GeoMutationResult<RegionModel>> UpdateRegionAsync(
		Guid actorUserId,
		string actorPassword,
		RegionUpdateModel model,
		CancellationToken cancellationToken);

	Task<GeoMutationResult<CityModel>> UpdateCityAsync(
		Guid actorUserId,
		string actorPassword,
		CityUpdateModel model,
		CancellationToken cancellationToken);

	Task<GeoMutationResult> DeleteRegionAsync(
		Guid actorUserId,
		string actorPassword,
		string regionName,
		CancellationToken cancellationToken);

	Task<GeoMutationResult> DeleteCityAsync(
		Guid actorUserId,
		string actorPassword,
		string regionName,
		string cityName,
		CancellationToken cancellationToken);
}