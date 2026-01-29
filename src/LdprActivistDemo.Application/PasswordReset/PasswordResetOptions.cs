namespace LdprActivistDemo.Application.PasswordReset;

public sealed class PasswordResetOptions
{
	public int TtlSeconds { get; init; } = 600;
}