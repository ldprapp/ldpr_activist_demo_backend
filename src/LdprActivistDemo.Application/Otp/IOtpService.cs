namespace LdprActivistDemo.Application.Otp;

public interface IOtpService
{
	Task IssueAsync(string phoneNumber, CancellationToken cancellationToken);
	Task<bool> VerifyAsync(string phoneNumber, string code, CancellationToken cancellationToken);
}