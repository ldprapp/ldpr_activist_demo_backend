using LdprActivistDemo.Application.Geo.Models;

namespace LdprActivistDemo.Application.Geo;

public interface ICityRepository
{
	Task<IReadOnlyList<CityModel>> GetByRegionAsync(string regionName, CancellationToken cancellationToken);
	Task<int?> GetIdByRegionAndNameAsync(string regionName, string cityName, CancellationToken cancellationToken);
	Task<GeoMutationResult<IReadOnlyList<CityModel>>> CreateManyAsync(string regionName, IReadOnlyList<string> names, CancellationToken cancellationToken);
	Task<GeoMutationResult<CityModel>> UpdateAsync(string regionName, string currentName, string newName, CancellationToken cancellationToken);
	Task<GeoMutationResult> DeleteAsync(string regionName, string cityName, CancellationToken cancellationToken);
}