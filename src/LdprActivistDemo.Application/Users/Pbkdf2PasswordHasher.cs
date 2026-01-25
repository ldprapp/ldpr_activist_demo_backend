using System.Security.Cryptography;
using System.Text;

namespace LdprActivistDemo.Application.Users;

public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
	private const string FormatMarker = "pbkdf2-sha256";
	private const int SaltSize = 16;
	private const int KeySize = 32;
	private const int Iterations = 600_000;

	public string Hash(string password)
	{
		if(string.IsNullOrWhiteSpace(password))
		{
			throw new ArgumentException("Password must be non-empty.", nameof(password));
		}

		var salt = RandomNumberGenerator.GetBytes(SaltSize);
		var key = DeriveKey(password, salt, Iterations, KeySize);

		return string.Concat(
			FormatMarker, "$",
			Iterations.ToString(), "$",
			Convert.ToBase64String(salt), "$",
			Convert.ToBase64String(key));
	}

	public bool Verify(string storedHash, string password)
	{
		if(string.IsNullOrEmpty(storedHash) || password is null)
		{
			return false;
		}

		if(!TryParse(storedHash, out var iterations, out var salt, out var expectedKey))
		{
			return StringComparer.Ordinal.Equals(storedHash, password);
		}

		var actualKey = DeriveKey(password, salt, iterations, expectedKey.Length);
		return CryptographicOperations.FixedTimeEquals(actualKey, expectedKey);
	}

	private static byte[] DeriveKey(string password, byte[] salt, int iterations, int outputLength)
	{
		var passwordBytes = Encoding.UTF8.GetBytes(password);
		try
		{
			return Rfc2898DeriveBytes.Pbkdf2(
				password: passwordBytes,
				salt: salt,
				iterations: iterations,
				hashAlgorithm: HashAlgorithmName.SHA256,
				outputLength: outputLength);
		}
		finally
		{
			Array.Clear(passwordBytes, 0, passwordBytes.Length);
		}
	}

	private static bool TryParse(string storedHash, out int iterations, out byte[] salt, out byte[] expectedKey)
	{
		iterations = 0;
		salt = Array.Empty<byte>();
		expectedKey = Array.Empty<byte>();

		var parts = storedHash.Split('$', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		if(parts.Length != 4)
		{
			return false;
		}

		if(!StringComparer.Ordinal.Equals(parts[0], FormatMarker))
		{
			return false;
		}

		if(!int.TryParse(parts[1], out iterations) || iterations <= 0)
		{
			return false;
		}

		try
		{
			salt = Convert.FromBase64String(parts[2]);
			expectedKey = Convert.FromBase64String(parts[3]);
		}
		catch
		{
			return false;
		}

		return salt.Length >= 8 && expectedKey.Length >= 16;
	}
}