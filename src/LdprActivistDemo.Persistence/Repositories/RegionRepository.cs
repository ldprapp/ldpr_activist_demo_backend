using LdprActivistDemo.Application.Geo;
using LdprActivistDemo.Application.Geo.Models;

using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace LdprActivistDemo.Persistence;

public sealed class RegionRepository : IRegionRepository
{
	private readonly AppDbContext _db;

	public RegionRepository(AppDbContext db)
	{
		_db = db;
	}

	public async Task<IReadOnlyList<RegionModel>> GetAllAsync(CancellationToken cancellationToken)
	{
		return await _db.Regions
			.AsNoTracking()
			.OrderBy(x => x.Name)
			.Select(x => new RegionModel(x.Name, x.IsDeleted))
			.ToListAsync(cancellationToken);
	}

	public Task<bool> ExistsByNameAsync(string regionName, CancellationToken cancellationToken)
	{
		var key = NormalizeName(regionName).ToLowerInvariant();

		return _db.Regions
			.AsNoTracking()
			.AnyAsync(x => x.Name.ToLower() == key && !x.IsDeleted, cancellationToken);
	}

	public Task<int?> GetIdByNameAsync(string regionName, CancellationToken cancellationToken)
	{
		var key = NormalizeName(regionName).ToLowerInvariant();

		return _db.Regions
			.AsNoTracking()
			.Where(x => x.Name.ToLower() == key && !x.IsDeleted)
			.Select(x => (int?)x.Id)
			.FirstOrDefaultAsync(cancellationToken);
	}

	public async Task<GeoMutationResult<RegionModel>> CreateAsync(string name, CancellationToken cancellationToken)
	{
		name = NormalizeName(name);
		var key = name.ToLowerInvariant();

		var exists = await _db.Regions
			.AsNoTracking()
			.AnyAsync(x => x.Name.ToLower() == key, cancellationToken);

		if(exists)
		{
			return GeoMutationResult<RegionModel>.Fail(GeoMutationError.Duplicate);
		}

		var entity = new Region
		{
			Name = name,
		};

		_db.Regions.Add(entity);

		try
		{
			await _db.SaveChangesAsync(cancellationToken);
		}
		catch(DbUpdateException ex) when(IsUniqueViolation(ex))
		{
			return GeoMutationResult<RegionModel>.Fail(GeoMutationError.Duplicate);
		}

		return GeoMutationResult<RegionModel>.Ok(new RegionModel(entity.Name, entity.IsDeleted));
	}

	public async Task<GeoMutationResult<RegionModel>> UpdateAsync(string currentName, string newName, CancellationToken cancellationToken)
	{
		currentName = NormalizeName(currentName);
		newName = NormalizeName(newName);

		var currentKey = currentName.ToLowerInvariant();
		var newKey = newName.ToLowerInvariant();

		var region = await _db.Regions
			.FirstOrDefaultAsync(x => x.Name.ToLower() == currentKey, cancellationToken);

		if(region is null)
		{
			return GeoMutationResult<RegionModel>.Fail(GeoMutationError.RegionNotFound);
		}

		var duplicate = await _db.Regions
			.AsNoTracking()
			.AnyAsync(x => x.Id != region.Id && x.Name.ToLower() == newKey, cancellationToken);

		if(duplicate)
		{
			return GeoMutationResult<RegionModel>.Fail(GeoMutationError.Duplicate);
		}

		region.Name = newName;

		try
		{
			await _db.SaveChangesAsync(cancellationToken);
		}
		catch(DbUpdateException ex) when(IsUniqueViolation(ex))
		{
			return GeoMutationResult<RegionModel>.Fail(GeoMutationError.Duplicate);
		}

		return GeoMutationResult<RegionModel>.Ok(new RegionModel(region.Name, region.IsDeleted));
	}

	public async Task<GeoMutationResult> DeleteAsync(string name, string? targetRegionName, CancellationToken cancellationToken)
	{
		name = NormalizeName(name);
		var key = name.ToLowerInvariant();

		var region = await _db.Regions
			.Include(x => x.Settlements)
			.FirstOrDefaultAsync(x => x.Name.ToLower() == key, cancellationToken);

		if(region is null)
		{
			return GeoMutationResult.Fail(GeoMutationError.RegionNotFound);
		}

		if(region.Settlements.Any(x => !x.IsDeleted))
		{
			return GeoMutationResult.Fail(GeoMutationError.HasActiveSettlements);
		}

		Region? targetRegion = null;

		if(!string.IsNullOrWhiteSpace(targetRegionName))
		{
			var targetKey = NormalizeName(targetRegionName).ToLowerInvariant();
			if(string.Equals(targetKey, key, StringComparison.Ordinal))
			{
				return GeoMutationResult.Fail(GeoMutationError.ValidationFailed);
			}

			targetRegion = await _db.Regions
				.FirstOrDefaultAsync(x => x.Name.ToLower() == targetKey && !x.IsDeleted, cancellationToken);

			if(targetRegion is null)
			{
				return GeoMutationResult.Fail(GeoMutationError.RegionNotFound);
			}
		}

		await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

		if(targetRegion is not null)
		{
			await _db.Tasks
				.Where(x => x.RegionId == region.Id && x.SettlementId == null)
				.ExecuteUpdateAsync(
					setters => setters.SetProperty(x => x.RegionId, targetRegion.Id),
					cancellationToken);
		}

		region.IsDeleted = true;
		await _db.SaveChangesAsync(cancellationToken);
		await tx.CommitAsync(cancellationToken);

		return GeoMutationResult.Success();
	}

	public async Task<GeoMutationResult> RestoreAsync(string name, CancellationToken cancellationToken)
	{
		name = NormalizeName(name);
		var key = name.ToLowerInvariant();

		var region = await _db.Regions
			.FirstOrDefaultAsync(x => x.Name.ToLower() == key, cancellationToken);

		if(region is null)
		{
			return GeoMutationResult.Fail(GeoMutationError.RegionNotFound);
		}

		region.IsDeleted = false;
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