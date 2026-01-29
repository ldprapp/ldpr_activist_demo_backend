namespace LdprActivistDemo.Application.PasswordReset;

public interface IPasswordResetRepository
{
	Task<bool> SetPasswordHashAsync(
		Guid userId,
		string passwordHash,
		CancellationToken cancellationToken);
}