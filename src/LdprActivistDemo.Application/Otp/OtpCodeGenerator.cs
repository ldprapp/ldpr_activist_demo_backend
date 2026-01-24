using System.Security.Cryptography;

namespace LdprActivistDemo.Application.Otp;

public sealed class OtpCodeGenerator : IOtpCodeGenerator
{
	public string GenerateDigits(int length)
	{
		if(length <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(length));
		}

		Span<byte> bytes = stackalloc byte[length];
		RandomNumberGenerator.Fill(bytes);

		var chars = new char[length];
		for(var i = 0; i < length; i++)
		{
			chars[i] = (char)('0' + (bytes[i] % 10));
		}

		return new string(chars);
	}
}