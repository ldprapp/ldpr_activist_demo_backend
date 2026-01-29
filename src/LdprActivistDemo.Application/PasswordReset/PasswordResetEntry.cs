namespace LdprActivistDemo.Application.PasswordReset;

public sealed record PasswordResetEntry(Guid UserId, string PasswordHash);