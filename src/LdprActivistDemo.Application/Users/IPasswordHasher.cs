namespace LdprActivistDemo.Application.Users;

public interface IPasswordHasher
{
	string Hash(string password);
	bool Verify(string storedHash, string password);
}