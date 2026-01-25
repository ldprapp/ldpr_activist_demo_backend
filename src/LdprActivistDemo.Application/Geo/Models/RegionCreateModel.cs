namespace LdprActivistDemo.Application.Geo.Models;

public sealed record RegionCreateModel(Guid ActorUserId, string ActorPasswordHash, string Name);
