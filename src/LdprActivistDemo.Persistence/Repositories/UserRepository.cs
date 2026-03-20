using LdprActivistDemo.Application.Users;
using LdprActivistDemo.Application.Users.Models;
using LdprActivistDemo.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

namespace LdprActivistDemo.Persistence;

public sealed class UserRepository : IUserRepository
{
	private readonly AppDbContext _db;
	private readonly IPasswordHasher _passwordHasher;

	public UserRepository(AppDbContext db, IPasswordHasher passwordHasher)
	{
		_db = db;
		_passwordHasher = passwordHasher;
	}

	public async Task<UserInternalModel?> GetInternalByIdAsync(Guid userId, CancellationToken cancellationToken)
	{
		var u = await _db.Users.AsNoTracking()
			.Include(x => x.Region)
			.Include(x => x.Settlement)
			.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
		return u is null ? null : ToInternal(u);
	}

	public async Task<UserInternalModel?> GetInternalByPhoneAsync(string phoneNumber, CancellationToken cancellationToken)
	{
		var u = await _db.Users.AsNoTracking()
			.Include(x => x.Region)
			.Include(x => x.Settlement)
			.FirstOrDefaultAsync(x => x.PhoneNumber == phoneNumber, cancellationToken);
		return u is null ? null : ToInternal(u);
	}

	public async Task<UserPublicModel?> GetPublicByIdAsync(Guid userId, CancellationToken cancellationToken)
	{
		var u = await _db.Users.AsNoTracking()
			.Include(x => x.Region)
			.Include(x => x.Settlement)
			.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
		return u is null ? null : ToPublic(u);
	}

	public async Task<UserPublicModel?> GetPublicByPhoneAsync(string phoneNumber, CancellationToken cancellationToken)
	{
		var u = await _db.Users.AsNoTracking()
			.Include(x => x.Region)
			.Include(x => x.Settlement)
			.FirstOrDefaultAsync(x => x.PhoneNumber == phoneNumber, cancellationToken);
		return u is null ? null : ToPublic(u);
	}

	public Task<bool> ExistsConfirmedByPhoneAsync(string phoneNumber, CancellationToken cancellationToken)
		=> _db.Users.AsNoTracking().AnyAsync(x => x.PhoneNumber == phoneNumber && x.IsPhoneConfirmed, cancellationToken);

	public async Task<bool> DeleteUnconfirmedByPhoneAsync(string phoneNumber, CancellationToken cancellationToken)
	{
		var entities = await _db.Users
		   .Where(x => x.PhoneNumber == phoneNumber && !x.IsPhoneConfirmed)
		   .ToListAsync(cancellationToken);

		if(entities.Count == 0)
		{
			return false;
		}

		_db.Users.RemoveRange(entities);
		await _db.SaveChangesAsync(cancellationToken);
		return true;
	}

	public async Task<bool> DeleteUnconfirmedByIdAsync(Guid userId, CancellationToken cancellationToken)
	{
		var entity = await _db.Users
			.FirstOrDefaultAsync(x => x.Id == userId && !x.IsPhoneConfirmed, cancellationToken);

		if(entity is null)
		{
			return false;
		}

		_db.Users.Remove(entity);
		await _db.SaveChangesAsync(cancellationToken);
		return true;
	}

	public async Task<int> DeleteAllUnconfirmedAsync(CancellationToken cancellationToken)
	{
		var entities = await _db.Users
			.Where(x => !x.IsPhoneConfirmed)
			.ToListAsync(cancellationToken);

		if(entities.Count == 0)
		{
			return 0;
		}

		_db.Users.RemoveRange(entities);
		await _db.SaveChangesAsync(cancellationToken);
		return entities.Count;
	}

	public async Task<Guid> CreateAsync(UserCreateModel model, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var gender = NormalizeGenderOrThrow(model.Gender);

		var geo = await ResolveRegionSettlementAsync(model.RegionName, model.SettlementName, cancellationToken);
		if(!geo.IsSuccess)
		{
			throw new InvalidOperationException("Settlement does not belong to Region or does not exist.");
		}

		var exists = await _db.Users.AsNoTracking()
			.AnyAsync(x => x.PhoneNumber == model.PhoneNumber && x.IsPhoneConfirmed, cancellationToken);

		if(exists)
		{
			throw new InvalidOperationException("PhoneNumber already exists.");
		}

		cancellationToken.ThrowIfCancellationRequested();

		var userId = Guid.NewGuid();
		var entity = new User
		{
			Id = userId,
			LastName = model.LastName,
			FirstName = model.FirstName,
			MiddleName = model.MiddleName,
			Gender = gender,
			PhoneNumber = model.PhoneNumber,
			PasswordHash = _passwordHasher.Hash(model.Password),
			BirthDate = model.BirthDate,
			RegionId = geo.RegionId,
			SettlementId = geo.SettlementId,
			Role = UserRoles.Activist,
			IsPhoneConfirmed = false,
			AvatarImageUrl = null,
		};

		_db.Users.Add(entity);
		_db.UserPointsTransactions.Add(new UserPointsTransaction
		{
			Id = Guid.NewGuid(),
			UserId = userId,
			Amount = 0,
			TransactionAt = DateTimeOffset.UtcNow,
			Comment = "User initialization transaction.",
		});
		_db.UserRatings.Add(new UserRating
		{
			UserId = userId,
			OverallRank = null,
			RegionRank = null,
			SettlementRank = null,
		});
		await _db.SaveChangesAsync(cancellationToken);
		return entity.Id;
	}

	public async Task<bool> ValidatePasswordAsync(string phoneNumber, string password, CancellationToken cancellationToken)
	{
		var u = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.PhoneNumber == phoneNumber, cancellationToken);
		if(u is null)
		{
			return false;
		}

		cancellationToken.ThrowIfCancellationRequested();

		return _passwordHasher.Verify(u.PasswordHash, password);
	}

	public async Task<bool> ValidatePasswordAsync(Guid userId, string password, CancellationToken cancellationToken)
	{
		var u = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
		if(u is null)
		{
			return false;
		}

		cancellationToken.ThrowIfCancellationRequested();

		return _passwordHasher.Verify(u.PasswordHash, password);
	}

	public async Task<bool> SetPhoneConfirmedAsync(string phoneNumber, bool isConfirmed, CancellationToken cancellationToken)
	{
		var u = await _db.Users.FirstOrDefaultAsync(x => x.PhoneNumber == phoneNumber, cancellationToken);
		if(u is null)
		{
			return false;
		}

		u.IsPhoneConfirmed = isConfirmed;
		await _db.SaveChangesAsync(cancellationToken);
		return true;
	}

	public async Task<bool> SetPasswordAsync(Guid userId, string newPassword, CancellationToken cancellationToken)
	{
		var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
		if(u is null)
		{
			return false;
		}

		cancellationToken.ThrowIfCancellationRequested();

		u.PasswordHash = _passwordHasher.Hash(newPassword);
		await _db.SaveChangesAsync(cancellationToken);
		return true;
	}

	public async Task<bool> SetAvatarImageAsync(Guid userId, Guid? avatarImageId, CancellationToken cancellationToken)
	{
		var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
		if(u is null)
		{
			return false;
		}

		var hadAvatar = ImageGcHelpers.TryExtractImageId(u.AvatarImageUrl, out var previousAvatarId);
		Guid? nextAvatarId = hadAvatar ? previousAvatarId : (Guid?)null;

		if(avatarImageId.HasValue && avatarImageId.Value != Guid.Empty)
		{
			u.AvatarImageUrl = avatarImageId.Value.ToString("D");
			nextAvatarId = avatarImageId.Value;
		}
		else
		{
			u.AvatarImageUrl = null;
			nextAvatarId = null;
		}

		await _db.SaveChangesAsync(cancellationToken);

		if(hadAvatar && nextAvatarId != previousAvatarId)
		{
			await ImageGcHelpers.DeleteOrphanManyAsync(_db, new[] { previousAvatarId }, cancellationToken);
		}

		return true;
	}

	public async Task<bool> UpdateAsync(UserUpdateModel model, CancellationToken cancellationToken)
	{
		var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == model.UserId, cancellationToken);
		if(u is null)
		{
			return false;
		}

		var hadAvatar = ImageGcHelpers.TryExtractImageId(u.AvatarImageUrl, out var previousAvatarId);
		Guid? newAvatarId = hadAvatar ? previousAvatarId : (Guid?)null;
		var avatarChanged = false;

		var geo = await ResolveRegionSettlementAsync(model.RegionName, model.SettlementName, cancellationToken);
		if(!geo.IsSuccess)
		{
			throw new InvalidOperationException("Settlement does not belong to Region or does not exist.");
		}

		u.LastName = model.LastName;
		u.FirstName = model.FirstName;
		u.MiddleName = model.MiddleName;
		u.Gender = NormalizeGenderOrThrow(model.Gender);
		u.BirthDate = model.BirthDate;
		u.RegionId = geo.RegionId;
		u.SettlementId = geo.SettlementId;

		if(model.AvatarImageId.HasValue)
		{
			avatarChanged = true;
			if(model.AvatarImageId.Value == Guid.Empty)
			{
				u.AvatarImageUrl = null;
				newAvatarId = null;
			}
			else
			{
				u.AvatarImageUrl = model.AvatarImageId.Value.ToString("D");
				newAvatarId = model.AvatarImageId.Value;
			}
		}

		await _db.SaveChangesAsync(cancellationToken);

		if(avatarChanged && hadAvatar && newAvatarId != previousAvatarId)
		{
			await ImageGcHelpers.DeleteOrphanManyAsync(_db, new[] { previousAvatarId }, cancellationToken);
		}

		return true;
	}

	public async Task<bool> ChangePhoneAsync(Guid userId, string newPhoneNumber, CancellationToken cancellationToken)
	{
		var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
		if(u is null)
		{
			return false;
		}

		var exists = await _db.Users.AsNoTracking().AnyAsync(x => x.PhoneNumber == newPhoneNumber && x.Id != userId, cancellationToken);
		if(exists)
		{
			throw new InvalidOperationException("PhoneNumber already exists.");
		}

		u.PhoneNumber = newPhoneNumber;
		u.IsPhoneConfirmed = false;
		await _db.SaveChangesAsync(cancellationToken);
		return true;
	}

	public Task<IReadOnlyList<UserPublicModel>> GetByRegionAsync(string regionName, CancellationToken cancellationToken) =>
		GetByFiltersAsync(role: null, regionName: regionName, settlementName: null, cancellationToken);

	public Task<IReadOnlyList<UserPublicModel>> GetBySettlementAsync(string settlementName, CancellationToken cancellationToken) =>
		GetByFiltersAsync(role: null, regionName: null, settlementName: settlementName, cancellationToken);

	public Task<IReadOnlyList<UserPublicModel>> GetByRegionAndSettlementAsync(string regionName, string settlementName, CancellationToken cancellationToken) =>
		GetByFiltersAsync(role: null, regionName: regionName, settlementName: settlementName, cancellationToken);

	public async Task<bool> SetRoleAsync(Guid userId, string role, CancellationToken cancellationToken)
	{
		if(userId == Guid.Empty)
		{
			return false;
		}

		if(!UserRoleRules.TryNormalizeRequiredRole(role, out var normalizedRole, out _))
		{
			throw new ArgumentException("Role is invalid.", nameof(role));
		}

		var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
		if(user is null)
		{
			return false;
		}

		user.Role = normalizedRole;
		await _db.SaveChangesAsync(cancellationToken);
		return true;
	}

	public async Task<string?> GetRoleAsync(Guid userId, CancellationToken cancellationToken)
	{
		if(userId == Guid.Empty)
		{
			return null;
		}

		return await _db.Users.AsNoTracking()
			.Where(x => x.Id == userId)
			.Select(x => (string?)x.Role)
			.FirstOrDefaultAsync(cancellationToken);
	}

	public async Task<IReadOnlyList<UserPublicModel>> GetByFiltersAsync(
		string? role,
		string? regionName,
		string? settlementName,
		CancellationToken cancellationToken)
	{
		IQueryable<User> query = _db.Users.AsNoTracking()
			.OrderBy(x => x.LastName)
			.ThenBy(x => x.FirstName)
			.ThenBy(x => x.MiddleName);

		if(!UserRoleRules.TryNormalizeOptionalRole(role, out var normalizedRole, out _))
		{
			return Array.Empty<UserPublicModel>();
		}

		if(normalizedRole is not null)
		{
			query = query.Where(x => x.Role == normalizedRole);
		}

		if(!string.IsNullOrWhiteSpace(regionName))
		{
			var regionKey = NormalizeName(regionName).ToLowerInvariant();
			query = query.Where(x => x.Region.Name.ToLower() == regionKey);
		}

		if(!string.IsNullOrWhiteSpace(settlementName))
		{
			var settlementKey = NormalizeName(settlementName).ToLowerInvariant();
			query = query.Where(x => x.Settlement.Name.ToLower() == settlementKey);
		}

		return await query
		   .Select(u => new UserPublicModel(
			   u.Id,
			   u.LastName,
			   u.FirstName,
			   u.MiddleName,
			   u.Gender,
			   u.PhoneNumber,
			   u.BirthDate,
			   u.Region.Name,
			   u.Settlement.Name,
			   u.Role,
			   u.IsPhoneConfirmed,
			   u.AvatarImageUrl))
		   .ToListAsync(cancellationToken);
	}

	private static string? NormalizeGenderOrThrow(string? value)
	{
		var token = (value ?? string.Empty).Trim();
		if(string.IsNullOrWhiteSpace(token))
		{
			return null;
		}

		token = token.ToLowerInvariant();

		return token switch
		{
			"m" or "male" or "man" or "м" or "муж" or "мужчина" => "male",
			"f" or "female" or "woman" or "ж" or "жен" or "женщина" => "female",
			_ => throw new InvalidOperationException("Gender is invalid."),
		};
	}

	private static UserInternalModel ToInternal(User u)
		=> new(
			u.Id,
			u.LastName,
			u.FirstName,
			u.MiddleName,
			u.Gender,
			u.PhoneNumber,
			u.PasswordHash,
			u.BirthDate,
			u.Region.Name,
			u.Settlement.Name,
			u.Role,
			u.IsPhoneConfirmed,
			u.AvatarImageUrl);

	private static UserPublicModel ToPublic(User u)
		=> new(
			u.Id,
			u.LastName,
			u.FirstName,
			u.MiddleName,
			u.Gender,
			u.PhoneNumber,
			u.BirthDate,
			u.Region.Name,
			u.Settlement.Name,
			u.Role,
			u.IsPhoneConfirmed,
			u.AvatarImageUrl);

	private async Task<(bool IsSuccess, int RegionId, int SettlementId)> ResolveRegionSettlementAsync(
		string regionName,
		string settlementName,
		CancellationToken cancellationToken)
	{
		var regionKey = NormalizeName(regionName).ToLowerInvariant();
		var settlementKey = NormalizeName(settlementName).ToLowerInvariant();

		var settlement = await _db.Settlements.AsNoTracking()
			.Where(x => x.Region.Name.ToLower() == regionKey && x.Name.ToLower() == settlementKey && !x.Region.IsDeleted && !x.IsDeleted)
			.Select(x => new { x.RegionId, x.Id })
			.FirstOrDefaultAsync(cancellationToken);

		return settlement is null
			? (false, 0, 0)
			: (true, settlement.RegionId, settlement.Id);
	}

	private static string NormalizeName(string? value)
		=> (value ?? string.Empty).Trim();
}