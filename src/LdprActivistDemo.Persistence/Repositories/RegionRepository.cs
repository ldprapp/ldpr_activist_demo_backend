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
			.Select(x => new RegionModel(x.Name))
			.ToListAsync(cancellationToken);
	}

	public Task<bool> ExistsByNameAsync(string regionName, CancellationToken cancellationToken)
	{
		var key = NormalizeName(regionName).ToLowerInvariant();

		return _db.Regions
			.AsNoTracking()
			.AnyAsync(x => x.Name.ToLower() == key, cancellationToken);
	}

	public Task<int?> GetIdByNameAsync(string regionName, CancellationToken cancellationToken)
	{
		var key = NormalizeName(regionName).ToLowerInvariant();

		return _db.Regions
			.AsNoTracking()
			.Where(x => x.Name.ToLower() == key)
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

		return GeoMutationResult<RegionModel>.Ok(new RegionModel(entity.Name));
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

		return GeoMutationResult<RegionModel>.Ok(new RegionModel(region.Name));
	}

	public async Task<GeoMutationResult> DeleteAsync(string name, CancellationToken cancellationToken)
	{
		name = NormalizeName(name);
		var key = name.ToLowerInvariant();

		var region = await _db.Regions
			.FirstOrDefaultAsync(x => x.Name.ToLower() == key, cancellationToken);

		if(region is null)
		{
			return GeoMutationResult.Fail(GeoMutationError.RegionNotFound);
		}

		_db.Regions.Remove(region);

		try
		{
			await _db.SaveChangesAsync(cancellationToken);
		}
		catch(DbUpdateException ex) when(IsForeignKeyViolation(ex))
		{
			return GeoMutationResult.Fail(GeoMutationError.InUse);
		}

		return GeoMutationResult.Success();
	}

	private static bool IsUniqueViolation(DbUpdateException ex)
	{
		return ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
	}

	private static bool IsForeignKeyViolation(DbUpdateException ex)
	{
		return ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation };
	}

	private static string NormalizeName(string? value)
		=> (value ?? string.Empty).Trim();
}