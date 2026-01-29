namespace LdprActivistDemo.Application.PasswordReset;

public interface IPasswordResetStore
{
	Task SetAsync(string phoneNumber, PasswordResetEntry entry, TimeSpan ttl, CancellationToken cancellationToken);
	Task<PasswordResetEntry?> GetAsync(string phoneNumber, CancellationToken cancellationToken);
	Task RemoveAsync(string phoneNumber, CancellationToken cancellationToken);
}