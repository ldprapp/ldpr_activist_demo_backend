namespace LdprActivistDemo.Application.Geo.Seeding;

public sealed class GeoSeedOptions
{
	public bool Enabled { get; init; } = true;

	public List<GeoSeedRegionOptions> Regions { get; init; } = new();
}

public sealed class GeoSeedRegionOptions
{
	public string Name { get; init; } = string.Empty;

	public List<string> Cities { get; init; } = new();
}