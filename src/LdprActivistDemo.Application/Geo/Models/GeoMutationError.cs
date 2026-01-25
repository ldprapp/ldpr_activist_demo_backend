namespace LdprActivistDemo.Application.Geo.Models;

public enum GeoMutationError
{
	None = 0,
	Unauthorized = 1,
	InvalidName = 2,
	RegionNotFound = 3,
	Duplicate = 4,
}