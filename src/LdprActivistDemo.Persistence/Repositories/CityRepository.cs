using LdprActivistDemo.Application.Geo;
using LdprActivistDemo.Application.Geo.Models;

using Microsoft.EntityFrameworkCore;

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
}