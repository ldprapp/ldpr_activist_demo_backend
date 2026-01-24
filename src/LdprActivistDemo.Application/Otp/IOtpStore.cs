namespace LdprActivistDemo.Application.Otp;

public interface IOtpStore
{
	Task SetAsync(string phoneNumber, string code, TimeSpan ttl, CancellationToken cancellationToken);
	Task<string?> GetAsync(string phoneNumber, CancellationToken cancellationToken);
	Task RemoveAsync(string phoneNumber, CancellationToken cancellationToken);
}