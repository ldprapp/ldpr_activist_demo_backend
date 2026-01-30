namespace LdprActivistDemo.Application.Images.Models;

public sealed record ImagePayload(Guid Id, string ContentType, byte[] Data);