using LdprActivistDemo.Application.Geo.Models;

namespace LdprActivistDemo.Application.Geo;

public interface IRegionRepository
{
	Task<IReadOnlyList<RegionModel>> GetAllAsync(CancellationToken cancellationToken);
	Task<bool> ExistsAsync(int regionId, CancellationToken cancellationToken);
	Task<RegionModel?> CreateAsync(string name, CancellationToken cancellationToken);
}