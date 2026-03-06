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
			.Include(x => x.City)
			.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
		return u is null ? null : ToInternal(u);
	}

	public async Task<UserInternalModel?> GetInternalByPhoneAsync(string phoneNumber, CancellationToken cancellationToken)
	{
		var u = await _db.Users.AsNoTracking()
			.Include(x => x.Region)
			.Include(x => x.City)
			.FirstOrDefaultAsync(x => x.PhoneNumber == phoneNumber, cancellationToken);
		return u is null ? null : ToInternal(u);
	}

	public async Task<UserPublicModel?> GetPublicByIdAsync(Guid userId, CancellationToken cancellationToken)
	{
		var u = await _db.Users.AsNoTracking()
			.Include(x => x.Region)
			.Include(x => x.City)
			.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
		return u is null ? null : ToPublic(u);
	}

	public async Task<UserPublicModel?> GetPublicByPhoneAsync(string phoneNumber, CancellationToken cancellationToken)
	{
		var u = await _db.Users.AsNoTracking()
			.Include(x => x.Region)
			.Include(x => x.City)
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
		var gender = NormalizeGenderOrThrow(model.Gender);

		var geo = await ResolveRegionCityAsync(model.RegionName, model.CityName, cancellationToken);
		if(!geo.IsSuccess)
		{
			throw new InvalidOperationException("City does not belong to Region or does not exist.");
		}

		var exists = await _db.Users.AsNoTracking()
			.AnyAsync(x => x.PhoneNumber == model.PhoneNumber && x.IsPhoneConfirmed, cancellationToken);

		if(exists)
		{
			throw new InvalidOperationException("PhoneNumber already exists.");
		}

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
			CityId = geo.CityId,
			IsAdmin = false,
			IsPhoneConfirmed = false,
			AvatarImageUrl = model.AvatarImageId.HasValue && model.AvatarImageId.Value != Guid.Empty
				? model.AvatarImageId.Value.ToString("D")
				: null,
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

		return _passwordHasher.Verify(u.PasswordHash, password);
	}

	public async Task<bool> ValidatePasswordAsync(Guid userId, string password, CancellationToken cancellationToken)
	{
		var u = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
		if(u is null)
		{
			return false;
		}

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

		u.PasswordHash = _passwordHasher.Hash(newPassword);
		await _db.SaveChangesAsync(cancellationToken);
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

		var geo = await ResolveRegionCityAsync(model.RegionName, model.CityName, cancellationToken);
		if(!geo.IsSuccess)
		{
			throw new InvalidOperationException("City does not belong to Region or does not exist.");
		}

		u.LastName = model.LastName;
		u.FirstName = model.FirstName;
		u.MiddleName = model.MiddleName;
		u.Gender = NormalizeGenderOrThrow(model.Gender);
		u.BirthDate = model.BirthDate;
		u.RegionId = geo.RegionId;
		u.CityId = geo.CityId;

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

	public async Task<IReadOnlyList<UserPublicModel>> GetByRegionAsync(string regionName, CancellationToken cancellationToken)
	{
		var regionKey = NormalizeName(regionName).ToLowerInvariant();

		return await _db.Users.AsNoTracking()
			.Where(x => x.Region.Name.ToLower() == regionKey)
			.OrderBy(x => x.LastName).ThenBy(x => x.FirstName).ThenBy(x => x.MiddleName)
			.Select(x => new UserPublicModel(
				x.Id,
				x.LastName,
				x.FirstName,
				x.MiddleName,
				x.Gender,
				x.PhoneNumber,
				x.BirthDate,
				x.Region.Name,
				x.City.Name,
				x.IsPhoneConfirmed,
				x.AvatarImageUrl))
			.ToListAsync(cancellationToken);
	}

	public async Task<IReadOnlyList<UserPublicModel>> GetByCityAsync(string cityName, CancellationToken cancellationToken)
	{
		var cityKey = NormalizeName(cityName).ToLowerInvariant();

		return await _db.Users.AsNoTracking()
			.Where(x => x.City.Name.ToLower() == cityKey)
			.OrderBy(x => x.LastName).ThenBy(x => x.FirstName).ThenBy(x => x.MiddleName)
			.Select(x => new UserPublicModel(
				x.Id,
				x.LastName,
				x.FirstName,
				x.MiddleName,
				x.Gender,
				x.PhoneNumber,
				x.BirthDate,
				x.Region.Name,
				x.City.Name,
				x.IsPhoneConfirmed,
				x.AvatarImageUrl))
			.ToListAsync(cancellationToken);
	}

	public async Task<IReadOnlyList<UserPublicModel>> GetByRegionAndCityAsync(string regionName, string cityName, CancellationToken cancellationToken)
	{
		var regionKey = NormalizeName(regionName).ToLowerInvariant();
		var cityKey = NormalizeName(cityName).ToLowerInvariant();

		return await _db.Users.AsNoTracking()
			.Where(x => x.Region.Name.ToLower() == regionKey && x.City.Name.ToLower() == cityKey)
			.OrderBy(x => x.LastName).ThenBy(x => x.FirstName).ThenBy(x => x.MiddleName)
			.Select(x => new UserPublicModel(
				x.Id,
				x.LastName,
				x.FirstName,
				x.MiddleName,
				x.Gender,
				x.PhoneNumber,
				x.BirthDate,
				x.Region.Name,
				x.City.Name,
 				x.IsPhoneConfirmed,
 				x.AvatarImageUrl))
 			.ToListAsync(cancellationToken);
	}

	public Task<bool> IsAdminAsync(Guid userId, CancellationToken cancellationToken)
		=> _db.Users.AsNoTracking().AnyAsync(x => x.Id == userId && x.IsAdmin, cancellationToken);

	public async Task<IReadOnlyList<Guid>> GetAllAdminIdsAsync(CancellationToken cancellationToken)
	{
		return await _db.Users.AsNoTracking()
			.Where(x => x.IsAdmin)
			.Select(x => x.Id)
			.ToListAsync(cancellationToken);
	}

	public async Task<IReadOnlyList<UserPublicModel>> GetAdminsAsync(int? start, int? end, CancellationToken cancellationToken)
	{
		IQueryable<User> query = _db.Users.AsNoTracking()
			.Where(x => x.IsAdmin)
			.OrderBy(x => x.LastName)
			.ThenBy(x => x.FirstName)
			.ThenBy(x => x.MiddleName);

		if(start is not null && end is not null)
		{
			var skip = start.Value - 1;
			var take = end.Value - start.Value + 1;

			query = query
				.Skip(skip)
				.Take(take);
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
				u.City.Name,
				u.IsPhoneConfirmed,
				u.AvatarImageUrl))
			.ToListAsync(cancellationToken);
	}

	public async Task<bool> AddPointsAsync(Guid userId, int pointsToAdd, CancellationToken cancellationToken)
	{
		if(pointsToAdd < 0)
		{
			throw new ArgumentOutOfRangeException(nameof(pointsToAdd));
		}

		var exists = await _db.Users.AsNoTracking().AnyAsync(x => x.Id == userId, cancellationToken);
		if(!exists)
		{
			return false;
		}

		_db.UserPointsTransactions.Add(new UserPointsTransaction
		{
			Id = Guid.NewGuid(),
			UserId = userId,
			Amount = pointsToAdd,
			TransactionAt = DateTimeOffset.UtcNow,
			Comment = "Points credited.",
		});
		await _db.SaveChangesAsync(cancellationToken);
		return true;
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
			u.City.Name,
			u.IsAdmin,
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
			u.City.Name,
			u.IsPhoneConfirmed,
			u.AvatarImageUrl);

	private async Task<(bool IsSuccess, int RegionId, int CityId)> ResolveRegionCityAsync(
		string regionName,
		string cityName,
		CancellationToken cancellationToken)
	{
		var regionKey = NormalizeName(regionName).ToLowerInvariant();
		var cityKey = NormalizeName(cityName).ToLowerInvariant();

		var city = await _db.Cities.AsNoTracking()
			.Where(x => x.Region.Name.ToLower() == regionKey && x.Name.ToLower() == cityKey)
			.Select(x => new { x.RegionId, x.Id })
			.FirstOrDefaultAsync(cancellationToken);

		return city is null
			? (false, 0, 0)
			: (true, city.RegionId, city.Id);
	}

	private static string NormalizeName(string? value)
		=> (value ?? string.Empty).Trim();
}