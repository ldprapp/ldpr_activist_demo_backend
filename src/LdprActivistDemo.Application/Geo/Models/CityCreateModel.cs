namespace LdprActivistDemo.Application.Geo.Models;

public sealed record CityCreateModel(Guid ActorUserId, string ActorPasswordHash, int RegionId, string Name);
