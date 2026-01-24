using LdprActivistDemo.Application.Geo.Models;

namespace LdprActivistDemo.Application.Geo;

public interface IRegionRepository
{
	Task<IReadOnlyList<RegionModel>> GetAllAsync(CancellationToken cancellationToken);
}