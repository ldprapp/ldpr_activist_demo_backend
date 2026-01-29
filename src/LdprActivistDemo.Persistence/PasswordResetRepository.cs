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
		var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
		if(u is null)
		{
			return false;
		}

		if(!u.IsPhoneConfirmed)
		{
			return false;
		}

		u.PasswordHash = passwordHash;
		await _db.SaveChangesAsync(cancellationToken);

		return true;
	}
}