using LdprActivistDemo.Application.PasswordReset;

using Microsoft.EntityFrameworkCore;

namespace LdprActivistDemo.Persistence;

public sealed class PasswordResetRepository : IPasswordResetRepository
{
	private readonly AppDbContext _db;

	public PasswordResetRepository(AppDbContext db)
	{
		_db = db ?? throw new ArgumentNullException(nameof(db));
	}

	public async Task<bool> SetPasswordHashAsync(Guid userId, string passwordHash, CancellationToken cancellationToken)
	{
		if(userId == Guid.Empty || string.IsNullOrWhiteSpace(passwordHash))
		{
			return false;
		}

		var affected = await _db.Users
			.Where(x => x.Id == userId && x.IsPhoneConfirmed)
			.ExecuteUpdateAsync(
				setters => setters.SetProperty(x => x.PasswordHash, passwordHash),
				cancellationToken);

		return affected > 0;
	}
}