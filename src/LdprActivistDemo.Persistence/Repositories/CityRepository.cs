using LdprActivistDemo.Application.Geo;
using LdprActivistDemo.Application.Geo.Models;

using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace LdprActivistDemo.Persistence;

public sealed class CityRepository : ICityRepository
{
	private readonly AppDbContext _db;

	public CityRepository(AppDbContext db)
	{
		_db = db;
	}

	public async Task<IReadOnlyList<CityModel>> GetByRegionAsync(string regionName, CancellationToken cancellationToken)
	{
		var regionKey = NormalizeName(regionName).ToLowerInvariant();

		return await _db.Cities
			.AsNoTracking()
			.Where(x => x.Region.Name.ToLower() == regionKey)
			.OrderBy(x => x.Name)
			.Select(x => new CityModel(x.Name))
 			.ToListAsync(cancellationToken);
	}

	public async Task<int?> GetIdByRegionAndNameAsync(string regionName, string cityName, CancellationToken cancellationToken)
	{
		var regionKey = NormalizeName(regionName).ToLowerInvariant();
		var cityKey = NormalizeName(cityName).ToLowerInvariant();

		return await _db.Cities
			.AsNoTracking()
			.Where(x => x.Region.Name.ToLower() == regionKey && x.Name.ToLower() == cityKey)
			.Select(x => (int?)x.Id)
			.FirstOrDefaultAsync(cancellationToken);
	}


	public async Task<GeoMutationResult<IReadOnlyList<CityModel>>> CreateManyAsync(
		string regionName,
		IReadOnlyList<string> names,
		CancellationToken cancellationToken)
	{
		var regionKey = NormalizeName(regionName).ToLowerInvariant();

		var region = await _db.Regions
			.AsNoTracking()
			.Where(x => x.Name.ToLower() == regionKey)
			.Select(x => new { x.Id })
			.FirstOrDefaultAsync(cancellationToken);

		if(region is null)
		{
			return GeoMutationResult<IReadOnlyList<CityModel>>.Fail(GeoMutationError.RegionNotFound);
		}

		var normalized = names
			.Select(NormalizeName)
			.ToList();

		var keys = normalized
			.Select(x => x.ToLowerInvariant())
			.ToHashSet(StringComparer.Ordinal);

		var exists = await _db.Cities
			.AsNoTracking()
			.AnyAsync(x => x.RegionId == region.Id && keys.Contains(x.Name.ToLower()), cancellationToken);

		if(exists)
		{
			return GeoMutationResult<IReadOnlyList<CityModel>>.Fail(GeoMutationError.Duplicate);
		}

		var entities = normalized
			.Select(name => new City
			{
				RegionId = region.Id,
				Name = name,
			})
			.ToList();

		_db.Cities.AddRange(entities);

		try
		{
			await _db.SaveChangesAsync(cancellationToken);
		}
		catch(DbUpdateException ex) when(IsUniqueViolation(ex))
		{
			return GeoMutationResult<IReadOnlyList<CityModel>>.Fail(GeoMutationError.Duplicate);
		}

		var created = entities
			.Select(x => new CityModel(x.Name))
			.ToList();

		return GeoMutationResult<IReadOnlyList<CityModel>>.Ok(created);
	}

	public async Task<GeoMutationResult<CityModel>> UpdateAsync(
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
			return GeoMutationResult<CityModel>.Fail(GeoMutationError.RegionNotFound);
		}

		var city = await _db.Cities
			.FirstOrDefaultAsync(x => x.RegionId == region.Id && x.Name.ToLower() == currentKey, cancellationToken);

		if(city is null)
		{
			return GeoMutationResult<CityModel>.Fail(GeoMutationError.CityNotFound);
		}

		var duplicate = await _db.Cities
			.AsNoTracking()
			.AnyAsync(x => x.RegionId == region.Id && x.Id != city.Id && x.Name.ToLower() == newKey, cancellationToken);

		if(duplicate)
		{
			return GeoMutationResult<CityModel>.Fail(GeoMutationError.Duplicate);
		}

		city.Name = NormalizeName(newName);

		try
		{
			await _db.SaveChangesAsync(cancellationToken);
		}
		catch(DbUpdateException ex) when(IsUniqueViolation(ex))
		{
			return GeoMutationResult<CityModel>.Fail(GeoMutationError.Duplicate);
		}

		return GeoMutationResult<CityModel>.Ok(new CityModel(city.Name));
	}

	public async Task<GeoMutationResult> DeleteAsync(
		string regionName,
		string cityName,
		CancellationToken cancellationToken)
	{
		var regionKey = NormalizeName(regionName).ToLowerInvariant();
		var cityKey = NormalizeName(cityName).ToLowerInvariant();

		var region = await _db.Regions
			.AsNoTracking()
			.Where(x => x.Name.ToLower() == regionKey)
			.Select(x => new { x.Id })
			.FirstOrDefaultAsync(cancellationToken);

		if(region is null)
		{
			return GeoMutationResult.Fail(GeoMutationError.RegionNotFound);
		}

		var city = await _db.Cities
			.FirstOrDefaultAsync(x => x.RegionId == region.Id && x.Name.ToLower() == cityKey, cancellationToken);

		if(city is null)
		{
			return GeoMutationResult.Fail(GeoMutationError.CityNotFound);
		}

		_db.Cities.Remove(city);

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