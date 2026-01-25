namespace LdprActivistDemo.Contracts.Geo;

public sealed record CreateCityRequest(Guid ActorUserId, string ActorPasswordHash, string Name);
