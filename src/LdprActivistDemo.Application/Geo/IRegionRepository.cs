using LdprActivistDemo.Application.Geo.Models;

namespace LdprActivistDemo.Application.Geo;

public interface IRegionRepository
{
	Task<IReadOnlyList<RegionModel>> GetAllAsync(CancellationToken cancellationToken);
	Task<bool> ExistsByNameAsync(string regionName, CancellationToken cancellationToken);
	Task<int?> GetIdByNameAsync(string regionName, CancellationToken cancellationToken);
	Task<GeoMutationResult<RegionModel>> CreateAsync(string name, CancellationToken cancellationToken);
	Task<GeoMutationResult<RegionModel>> UpdateAsync(string currentName, string newName, CancellationToken cancellationToken);
	Task<GeoMutationResult> DeleteAsync(string name, string? targetRegionName, CancellationToken cancellationToken);
	Task<GeoMutationResult> RestoreAsync(string name, CancellationToken cancellationToken);
}