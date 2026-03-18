using LdprActivistDemo.Application.Geo;
using LdprActivistDemo.Application.Geo.Models;

using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace LdprActivistDemo.Persistence;

public sealed class SettlementRepository : ISettlementRepository
{
	private readonly AppDbContext _db;

	public SettlementRepository(AppDbContext db)
	{
		_db = db;
	}

	public async Task<IReadOnlyList<SettlementModel>> GetByRegionAsync(string regionName, CancellationToken cancellationToken)
	{
		var regionKey = NormalizeName(regionName).ToLowerInvariant();

		return await _db.Settlements
 			.AsNoTracking()
 			.Where(x => x.Region.Name.ToLower() == regionKey)
 			.OrderBy(x => x.Name)
			.Select(x => new SettlementModel(x.Name, x.IsDeleted))
  			.ToListAsync(cancellationToken);
	}

	public async Task<int?> GetIdByRegionAndNameAsync(string regionName, string settlementName, CancellationToken cancellationToken)
	{
		var regionKey = NormalizeName(regionName).ToLowerInvariant();
		var settlementKey = NormalizeName(settlementName).ToLowerInvariant();

		return await _db.Settlements
 			.AsNoTracking()
			.Where(x => x.Region.Name.ToLower() == regionKey && x.Name.ToLower() == settlementKey && !x.Region.IsDeleted && !x.IsDeleted)
 			.Select(x => (int?)x.Id)
 			.FirstOrDefaultAsync(cancellationToken);
	}


	public async Task<GeoMutationResult<IReadOnlyList<SettlementModel>>> CreateManyAsync(
 		string regionName,
 		IReadOnlyList<string> names,
 		CancellationToken cancellationToken)
	{
		var regionKey = NormalizeName(regionName).ToLowerInvariant();

		var region = await _db.Regions
			.AsNoTracking()
			.Where(x => x.Name.ToLower() == regionKey)
			.Select(x => new { x.Id, x.IsDeleted })
			.FirstOrDefaultAsync(cancellationToken);

		if(region is null)
		{
			return GeoMutationResult<IReadOnlyList<SettlementModel>>.Fail(GeoMutationError.RegionNotFound);
		}

		if(region.IsDeleted)
		{
			return GeoMutationResult<IReadOnlyList<SettlementModel>>.Fail(GeoMutationError.ParentRegionDeleted);
		}

		var normalized = names
			.Select(NormalizeName)
			.ToList();

		var keys = normalized
			.Select(x => x.ToLowerInvariant())
			.ToHashSet(StringComparer.Ordinal);

		var exists = await _db.Settlements
			.AsNoTracking()
			.AnyAsync(x => x.RegionId == region.Id && keys.Contains(x.Name.ToLower()), cancellationToken);

		if(exists)
		{
			return GeoMutationResult<IReadOnlyList<SettlementModel>>.Fail(GeoMutationError.Duplicate);
		}

		var entities = normalized
			.Select(name => new Settlement
			{
				RegionId = region.Id,
				Name = name,
			})
			.ToList();

		_db.Settlements.AddRange(entities);

		try
		{
			await _db.SaveChangesAsync(cancellationToken);
		}
		catch(DbUpdateException ex) when(IsUniqueViolation(ex))
		{
			return GeoMutationResult<IReadOnlyList<SettlementModel>>.Fail(GeoMutationError.Duplicate);
		}

		var created = entities
			.Select(x => new SettlementModel(x.Name, x.IsDeleted))
			.ToList();

		return GeoMutationResult<IReadOnlyList<SettlementModel>>.Ok(created);
	}

	public async Task<GeoMutationResult<SettlementModel>> UpdateAsync(
 		string regionName,
 		string currentName,
 		string newName,
 		CancellationToken cancellationToken)
	{
		var regionKey = NormalizeName(regionName).ToLowerInvariant();
		var currentKey = NormalizeName(currentName).ToLowerInvariant();
		var newKey = NormalizeName(newName).ToLowerInvariant();

		var region = await _db.Regions
			.AsNoTracking()
			.Where(x => x.Name.ToLower() == regionKey)
			.Select(x => new { x.Id })
			.FirstOrDefaultAsync(cancellationToken);

		if(region is null)
		{
			return GeoMutationResult<SettlementModel>.Fail(GeoMutationError.RegionNotFound);
		}

		var settlement = await _db.Settlements
 			.FirstOrDefaultAsync(x => x.RegionId == region.Id && x.Name.ToLower() == currentKey, cancellationToken);

		if(settlement is null)
		{
			return GeoMutationResult<SettlementModel>.Fail(GeoMutationError.SettlementNotFound);
		}

		var duplicate = await _db.Settlements
 			.AsNoTracking()
			.AnyAsync(x => x.RegionId == region.Id && x.Id != settlement.Id && x.Name.ToLower() == newKey, cancellationToken);

		if(duplicate)
		{
			return GeoMutationResult<SettlementModel>.Fail(GeoMutationError.Duplicate);
		}

		settlement.Name = NormalizeName(newName);

		try
		{
			await _db.SaveChangesAsync(cancellationToken);
		}
		catch(DbUpdateException ex) when(IsUniqueViolation(ex))
		{
			return GeoMutationResult<SettlementModel>.Fail(GeoMutationError.Duplicate);
		}

		return GeoMutationResult<SettlementModel>.Ok(new SettlementModel(settlement.Name, settlement.IsDeleted));
	}

	public async Task<GeoMutationResult> DeleteAsync(
		string regionName,
	   string settlementName,
		string? targetRegionName,
	   string? targetSettlementName,
		CancellationToken cancellationToken)
	{
		var regionKey = NormalizeName(regionName).ToLowerInvariant();
		var settlementKey = NormalizeName(settlementName).ToLowerInvariant();

		var region = await _db.Regions
			.Where(x => x.Name.ToLower() == regionKey)
			.FirstOrDefaultAsync(cancellationToken);

		if(region is null)
		{
			return GeoMutationResult.Fail(GeoMutationError.RegionNotFound);
		}

		var settlement = await _db.Settlements
			.FirstOrDefaultAsync(x => x.RegionId == region.Id && x.Name.ToLower() == settlementKey, cancellationToken);

		if(settlement is null)
		{
			return GeoMutationResult.Fail(GeoMutationError.SettlementNotFound);
		}

		var hasTargetRegion = !string.IsNullOrWhiteSpace(targetRegionName);
		var hasTargetSettlement = !string.IsNullOrWhiteSpace(targetSettlementName);

		if(hasTargetRegion != hasTargetSettlement)
		{
			return GeoMutationResult.Fail(GeoMutationError.ValidationFailed);
		}

		Region? targetRegion = null;
		Settlement? targetSettlement = null;

		if(hasTargetRegion)
		{
			var targetRegionKey = NormalizeName(targetRegionName).ToLowerInvariant();
			var targetSettlementKey = NormalizeName(targetSettlementName).ToLowerInvariant();

			if(string.Equals(targetRegionKey, regionKey, StringComparison.Ordinal)
			   && string.Equals(targetSettlementKey, settlementKey, StringComparison.Ordinal))
			{
				return GeoMutationResult.Fail(GeoMutationError.ValidationFailed);
			}

			targetRegion = await _db.Regions
				.FirstOrDefaultAsync(x => x.Name.ToLower() == targetRegionKey && !x.IsDeleted, cancellationToken);

			if(targetRegion is null)
			{
				return GeoMutationResult.Fail(GeoMutationError.RegionNotFound);
			}

			targetSettlement = await _db.Settlements
 				.FirstOrDefaultAsync(
 					x => x.RegionId == targetRegion.Id
						&& x.Name.ToLower() == targetSettlementKey
 						&& !x.IsDeleted,
 					cancellationToken);

			if(targetSettlement is null)
			{
				return GeoMutationResult.Fail(GeoMutationError.SettlementNotFound);
			}
		}

		await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

		if(targetRegion is not null && targetSettlement is not null)
		{
			await _db.Users
			   .Where(x => x.SettlementId == settlement.Id)
				.ExecuteUpdateAsync(
					setters => setters
						.SetProperty(x => x.RegionId, targetRegion.Id)
					   .SetProperty(x => x.SettlementId, targetSettlement.Id),
					cancellationToken);

			await _db.Tasks
			   .Where(x => x.SettlementId == settlement.Id)
				.ExecuteUpdateAsync(
					setters => setters
						.SetProperty(x => x.RegionId, targetRegion.Id)
					   .SetProperty(x => x.SettlementId, targetSettlement.Id),
					cancellationToken);
		}

		settlement.IsDeleted = true;
		await _db.SaveChangesAsync(cancellationToken);
		await tx.CommitAsync(cancellationToken);

		return GeoMutationResult.Success();
	}

	public async Task<GeoMutationResult> RestoreAsync(
		string regionName,
	   string settlementName,
		CancellationToken cancellationToken)
	{
		var regionKey = NormalizeName(regionName).ToLowerInvariant();
		var settlementKey = NormalizeName(settlementName).ToLowerInvariant();

		var region = await _db.Regions
			.FirstOrDefaultAsync(x => x.Name.ToLower() == regionKey, cancellationToken);

		if(region is null)
		{
			return GeoMutationResult.Fail(GeoMutationError.RegionNotFound);
		}

		if(region.IsDeleted)
		{
			return GeoMutationResult.Fail(GeoMutationError.ParentRegionDeleted);
		}

		var settlement = await _db.Settlements
			.FirstOrDefaultAsync(x => x.RegionId == region.Id && x.Name.ToLower() == settlementKey, cancellationToken);

		if(settlement is null)
		{
			return GeoMutationResult.Fail(GeoMutationError.SettlementNotFound);
		}

		settlement.IsDeleted = false;
		await _db.SaveChangesAsync(cancellationToken);

		return GeoMutationResult.Success();
	}

	private static bool IsUniqueViolation(DbUpdateException ex)
	{
		return ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
	}

	private static string NormalizeName(string? value)
		=> (value ?? string.Empty).Trim();
}