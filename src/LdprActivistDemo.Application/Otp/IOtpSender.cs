namespace LdprActivistDemo.Application.Otp;

public interface IOtpSender
{
	Task SendAsync(string phoneNumber, string code, CancellationToken cancellationToken);
}