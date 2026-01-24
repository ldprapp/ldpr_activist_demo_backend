namespace LdprActivistDemo.Application.Otp;

public sealed class OtpOptions
{
	public int TtlSeconds { get; init; } = 300;

	public int Length { get; init; } = 6;
}