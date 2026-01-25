using LdprActivistDemo.Application.Geo.Seeding;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LdprActivistDemo.Persistence;

public sealed class GeoDbSeeder
{
	private readonly AppDbContext _db;
	private readonly IOptions<GeoSeedOptions> _options;
	private readonly ILogger<GeoDbSeeder> _logger;

	public GeoDbSeeder(AppDbContext db, IOptions<GeoSeedOptions> options, ILogger<GeoDbSeeder> logger)
	{
		_db = db;
		_options = options;
		_logger = logger;
	}

	public async Task SeedAsync(CancellationToken cancellationToken)
	{
		var opts = _options.Value;

		if(!opts.Enabled)
		{
			_logger.LogInformation("Geo DB seeding is disabled (GeoSeed:Enabled=false).");
			return;
		}

		var desired = NormalizeConfig(opts);
		if(desired.Count == 0)
		{
			_logger.LogInformation("Geo DB seeding skipped: GeoSeed:Regions is empty or invalid.");
			return;
		}

		var regionEntities = await _db.Regions.ToListAsync(cancellationToken);
		var regionByName = regionEntities.ToDictionary(x => NormalizeName(x.Name), StringComparer.OrdinalIgnoreCase);

		var regionsAdded = 0;
		foreach(var r in desired)
		{
			var key = NormalizeName(r.Name);
			if(regionByName.ContainsKey(key))
			{
				continue;
			}

			var entity = new Region { Name = r.Name };
			_db.Regions.Add(entity);
			regionByName.Add(key, entity);
			regionsAdded++;
		}

		if(regionsAdded > 0)
		{
			await _db.SaveChangesAsync(cancellationToken);
		}

		var existingCities = await _db.Cities
			.AsNoTracking()
			.Select(c => new { c.RegionId, c.Name })
			.ToListAsync(cancellationToken);

		var cityNamesByRegionId = existingCities
			.GroupBy(x => x.RegionId)
			.ToDictionary(
				g => g.Key,
				g => new HashSet<string>(g.Select(x => NormalizeName(x.Name)), StringComparer.OrdinalIgnoreCase));

		var citiesAdded = 0;

		foreach(var r in desired)
		{
			var region = regionByName[NormalizeName(r.Name)];

			if(!cityNamesByRegionId.TryGetValue(region.Id, out var existingNames))
			{
				existingNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
				cityNamesByRegionId[region.Id] = existingNames;
			}

			foreach(var city in r.Cities)
			{
				var cityKey = NormalizeName(city);
				if(!existingNames.Add(cityKey))
				{
					continue;
				}

				_db.Cities.Add(new City
				{
					RegionId = region.Id,
					Name = city,
				});

				citiesAdded++;
			}
		}

		if(citiesAdded > 0)
		{
			await _db.SaveChangesAsync(cancellationToken);
		}

		_logger.LogInformation(
			"Geo DB seeding finished. Regions added: {RegionsAdded}. Cities added: {CitiesAdded}.",
			regionsAdded,
			citiesAdded);
	}

	private static List<NormalizedRegion> NormalizeConfig(GeoSeedOptions opts)
	{
		var map = new Dictionary<string, NormalizedRegionBuilder>(StringComparer.OrdinalIgnoreCase);

		foreach(var r in opts.Regions ?? new List<GeoSeedRegionOptions>())
		{
			var regionName = NormalizeName(r.Name);
			if(string.IsNullOrWhiteSpace(regionName))
			{
				continue;
			}

			if(!map.TryGetValue(regionName, out var builder))
			{
				builder = new NormalizedRegionBuilder(regionName);
				map.Add(regionName, builder);
			}

			foreach(var city in r.Cities ?? new List<string>())
			{
				var cityName = NormalizeName(city);
				if(string.IsNullOrWhiteSpace(cityName))
				{
					continue;
				}

				builder.Cities.Add(cityName);
			}
		}

		return map.Values
			.Select(x => new NormalizedRegion(x.Name, x.Cities.OrderBy(c => c, StringComparer.OrdinalIgnoreCase).ToList()))
			.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
			.ToList();
	}

	private static string NormalizeName(string? value)
		=> (value ?? string.Empty).Trim();

	private sealed class NormalizedRegionBuilder
	{
		public NormalizedRegionBuilder(string name)
		{
			Name = name;
		}

		public string Name { get; }
		public HashSet<string> Cities { get; } = new(StringComparer.OrdinalIgnoreCase);
	}

	private sealed record NormalizedRegion(string Name, IReadOnlyList<string> Cities);
}