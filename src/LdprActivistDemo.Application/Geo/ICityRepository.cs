using LdprActivistDemo.Application.Geo.Models;

namespace LdprActivistDemo.Application.Geo;

public interface ICityRepository
{
	Task<IReadOnlyList<CityModel>> GetByRegionAsync(int regionId, CancellationToken cancellationToken);
}