namespace LdprActivistDemo.Application.Geo.Models;

public sealed record CityModel(
	int Id,
	int RegionId,
	string Name);