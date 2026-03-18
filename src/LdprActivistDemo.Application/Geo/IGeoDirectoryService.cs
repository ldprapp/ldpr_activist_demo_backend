using LdprActivistDemo.Application.Geo.Models;

namespace LdprActivistDemo.Application.Geo;

public interface IGeoDirectoryService
{
	Task<IReadOnlyList<RegionModel>> GetRegionsAsync(CancellationToken cancellationToken);
	Task<IReadOnlyList<SettlementModel>> GetSettlementsByRegionAsync(string regionName, CancellationToken cancellationToken);

	Task<GeoMutationResult<RegionModel>> CreateRegionAsync(
		Guid actorUserId,
		string actorPassword,
		RegionCreateModel model,
		CancellationToken cancellationToken);

	Task<GeoMutationResult<IReadOnlyList<SettlementModel>>> CreateSettlementsAsync(
 		Guid actorUserId,
 		string actorPassword,
 		string regionName,
		IReadOnlyList<string> settlementNames,
 		CancellationToken cancellationToken);

	Task<GeoMutationResult<RegionModel>> UpdateRegionAsync(
		Guid actorUserId,
		string actorPassword,
		RegionUpdateModel model,
		CancellationToken cancellationToken);

	Task<GeoMutationResult<SettlementModel>> UpdateSettlementAsync(
 		Guid actorUserId,
 		string actorPassword,
		SettlementUpdateModel model,
 		CancellationToken cancellationToken);

	Task<GeoMutationResult> DeleteRegionAsync(
	   Guid actorUserId,
		string actorPassword,
		RegionDeleteModel model,
		CancellationToken cancellationToken);

	Task<GeoMutationResult> DeleteSettlementAsync(
 		Guid actorUserId,
 		string actorPassword,
		SettlementDeleteModel model,
 		CancellationToken cancellationToken);

	Task<GeoMutationResult> RestoreRegionAsync(
		Guid actorUserId,
		string actorPassword,
		string regionName,
		CancellationToken cancellationToken);

	Task<GeoMutationResult> RestoreSettlementAsync(
 		Guid actorUserId,
 		string actorPassword,
 		string regionName,
		string settlementName,
 		CancellationToken cancellationToken);
}