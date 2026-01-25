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

	public async Task<IReadOnlyList<CityModel>> GetByRegionAsync(int regionId, CancellationToken cancellationToken)
	{
		return await _db.Cities
			.AsNoTracking()
			.Where(x => x.RegionId == regionId)
			.OrderBy(x => x.Name)
			.Select(x => new CityModel(x.Id, x.RegionId, x.Name))
			.ToListAsync(cancellationToken);
	}

	public async Task<CityModel?> CreateAsync(int regionId, string name, CancellationToken cancellationToken)
	{
		name = (name ?? string.Empty).Trim();

		var exists = await _db.Cities
			.AsNoTracking()
			.AnyAsync(x => x.RegionId == regionId && x.Name == name, cancellationToken);

		if(exists)
		{
			return null;
		}

		var entity = new City
		{
			RegionId = regionId,
			Name = name,
		};

		_db.Cities.Add(entity);

		try
		{
			await _db.SaveChangesAsync(cancellationToken);
		}
		catch(DbUpdateException ex) when(IsUniqueViolation(ex))
		{
			return null;
		}

		return new CityModel(entity.Id, entity.RegionId, entity.Name);
	}

	private static bool IsUniqueViolation(DbUpdateException ex)
	{
		return ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
	}
}