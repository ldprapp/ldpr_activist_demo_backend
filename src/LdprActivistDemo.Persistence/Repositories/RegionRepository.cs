using LdprActivistDemo.Application.Geo;
using LdprActivistDemo.Application.Geo.Models;

using Microsoft.EntityFrameworkCore;

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
}