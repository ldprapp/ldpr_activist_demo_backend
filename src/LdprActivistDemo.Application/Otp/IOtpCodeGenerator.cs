namespace LdprActivistDemo.Application.Otp;

public interface IOtpCodeGenerator
{
	string GenerateDigits(int length);
}