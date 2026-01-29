using LdprActivistDemo.Application.Users;
using LdprActivistDemo.Application.Users.Models;

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
		var u = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
		return u is null ? null : ToInternal(u);
	}

	public async Task<UserInternalModel?> GetInternalByPhoneAsync(string phoneNumber, CancellationToken cancellationToken)
	{
		var u = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.PhoneNumber == phoneNumber, cancellationToken);
		return u is null ? null : ToInternal(u);
	}

	public async Task<UserPublicModel?> GetPublicByIdAsync(Guid userId, CancellationToken cancellationToken)
	{
		var u = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
		return u is null ? null : ToPublic(u);
	}

	public async Task<UserPublicModel?> GetPublicByPhoneAsync(string phoneNumber, CancellationToken cancellationToken)
	{
		var u = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.PhoneNumber == phoneNumber, cancellationToken);
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

		var city = await _db.Cities.AsNoTracking().FirstOrDefaultAsync(x => x.Id == model.CityId, cancellationToken);
		if(city is null || city.RegionId != model.RegionId)
		{
			throw new InvalidOperationException("City does not belong to Region or does not exist.");
		}

		var exists = await _db.Users.AsNoTracking()
			.AnyAsync(x => x.PhoneNumber == model.PhoneNumber && x.IsPhoneConfirmed, cancellationToken);

		if(exists)
		{
			throw new InvalidOperationException("PhoneNumber already exists.");
		}

		var entity = new User
		{
			Id = Guid.NewGuid(),
			LastName = model.LastName,
			FirstName = model.FirstName,
			MiddleName = model.MiddleName,
			Gender = gender,
			PhoneNumber = model.PhoneNumber,
			PasswordHash = _passwordHasher.Hash(model.Password),
			BirthDate = model.BirthDate,
			RegionId = model.RegionId,
			CityId = model.CityId,
			IsAdmin = false,
			IsPhoneConfirmed = false,
			Points = 0,
		};

		_db.Users.Add(entity);
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

	public async Task<bool> ChangePasswordAsync(Guid userId, string oldPassword, string newPassword, CancellationToken cancellationToken)
	{
		var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
		if(u is null)
		{
			return false;
		}

		if(!_passwordHasher.Verify(u.PasswordHash, oldPassword))
		{
			return false;
		}

		u.PasswordHash = _passwordHasher.Hash(newPassword);
		await _db.SaveChangesAsync(cancellationToken);
		return true;
	}

	public async Task<bool> UpdateAsync(UserUpdateModel model, string actorPassword, CancellationToken cancellationToken)
	{
		var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == model.UserId, cancellationToken);
		if(u is null)
		{
			return false;
		}

		if(!_passwordHasher.Verify(u.PasswordHash, actorPassword))
		{
			return false;
		}

		var city = await _db.Cities.AsNoTracking().FirstOrDefaultAsync(x => x.Id == model.CityId, cancellationToken);
		if(city is null || city.RegionId != model.RegionId)
		{
			throw new InvalidOperationException("City does not belong to Region or does not exist.");
		}

		u.LastName = model.LastName;
		u.FirstName = model.FirstName;
		u.MiddleName = model.MiddleName;
		u.Gender = NormalizeGenderOrThrow(model.Gender);
		u.BirthDate = model.BirthDate;
		u.RegionId = model.RegionId;
		u.CityId = model.CityId;

		await _db.SaveChangesAsync(cancellationToken);
		return true;
	}

	public async Task<bool> ChangePhoneAsync(Guid userId, string password, string newPhoneNumber, CancellationToken cancellationToken)
	{
		var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
		if(u is null)
		{
			return false;
		}

		if(!_passwordHasher.Verify(u.PasswordHash, password))
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

	public async Task<IReadOnlyList<UserFullNameModel>> GetByRegionAsync(int regionId, CancellationToken cancellationToken)
	{
		return await _db.Users.AsNoTracking()
			.Where(x => x.RegionId == regionId)
			.OrderBy(x => x.LastName).ThenBy(x => x.FirstName).ThenBy(x => x.MiddleName)
			.Select(x => new UserFullNameModel(x.Id, x.LastName, x.FirstName, x.MiddleName))
			.ToListAsync(cancellationToken);
	}

	public async Task<IReadOnlyList<UserFullNameModel>> GetByCityAsync(int cityId, CancellationToken cancellationToken)
	{
		return await _db.Users.AsNoTracking()
			.Where(x => x.CityId == cityId)
			.OrderBy(x => x.LastName).ThenBy(x => x.FirstName).ThenBy(x => x.MiddleName)
			.Select(x => new UserFullNameModel(x.Id, x.LastName, x.FirstName, x.MiddleName))
			.ToListAsync(cancellationToken);
	}

	public async Task<IReadOnlyList<UserFullNameModel>> GetByRegionAndCityAsync(int regionId, int cityId, CancellationToken cancellationToken)
	{
		return await _db.Users.AsNoTracking()
			.Where(x => x.RegionId == regionId && x.CityId == cityId)
			.OrderBy(x => x.LastName).ThenBy(x => x.FirstName).ThenBy(x => x.MiddleName)
			.Select(x => new UserFullNameModel(x.Id, x.LastName, x.FirstName, x.MiddleName))
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

	public async Task<bool> AddPointsAsync(Guid userId, int pointsToAdd, CancellationToken cancellationToken)
	{
		if(pointsToAdd < 0)
		{
			throw new ArgumentOutOfRangeException(nameof(pointsToAdd));
		}

		var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
		if(u is null)
		{
			return false;
		}

		u.Points += pointsToAdd;
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
			u.RegionId,
			u.CityId,
			u.IsAdmin,
			u.IsPhoneConfirmed,
			u.Points);

	private static UserPublicModel ToPublic(User u)
		=> new(
			u.Id,
			u.LastName,
			u.FirstName,
			u.MiddleName,
			u.Gender,
			u.PhoneNumber,
			u.BirthDate,
			u.RegionId,
			u.CityId,
			u.IsPhoneConfirmed,
			u.Points);
}