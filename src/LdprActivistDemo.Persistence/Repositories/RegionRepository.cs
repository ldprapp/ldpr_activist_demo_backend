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
			.Select(x => new RegionModel(x.Id, x.Name))
			.ToListAsync(cancellationToken);
	}

	public Task<bool> ExistsAsync(int regionId, CancellationToken cancellationToken)
	{
		return _db.Regions
			.AsNoTracking()
			.AnyAsync(x => x.Id == regionId, cancellationToken);
	}

	public async Task<RegionModel?> CreateAsync(string name, CancellationToken cancellationToken)
	{
		name = (name ?? string.Empty).Trim();

		var exists = await _db.Regions
			.AsNoTracking()
			.AnyAsync(x => x.Name == name, cancellationToken);

		if(exists)
		{
			return null;
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
			return null;
		}

		return new RegionModel(entity.Id, entity.Name);
	}

	private static bool IsUniqueViolation(DbUpdateException ex)
	{
		return ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
	}
}