namespace LdprActivistDemo.Contracts.Geo;

public sealed record CreateRegionRequest(Guid ActorUserId, string ActorPasswordHash, string Name);
