using LdprActivistDemo.Application.Geo.Models;

namespace LdprActivistDemo.Application.Geo;

public interface ISettlementRepository
{
	Task<IReadOnlyList<SettlementModel>> GetByRegionAsync(string regionName, CancellationToken cancellationToken);
	Task<int?> GetIdByRegionAndNameAsync(string regionName, string settlementName, CancellationToken cancellationToken);
	Task<GeoMutationResult<IReadOnlyList<SettlementModel>>> CreateManyAsync(string regionName, IReadOnlyList<string> names, CancellationToken cancellationToken);
	Task<GeoMutationResult<SettlementModel>> UpdateAsync(string regionName, string currentName, string newName, CancellationToken cancellationToken);
	Task<GeoMutationResult> DeleteAsync(string regionName, string settlementName, string? targetRegionName, string? targetSettlementName, CancellationToken cancellationToken);
	Task<GeoMutationResult> RestoreAsync(string regionName, string settlementName, CancellationToken cancellationToken);
}