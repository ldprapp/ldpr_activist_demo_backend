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

	public async Task<Guid> CreateAsync(UserCreateModel model, CancellationToken cancellationToken)
	{
		var city = await _db.Cities.AsNoTracking().FirstOrDefaultAsync(x => x.Id == model.CityId, cancellationToken);
		if(city is null || city.RegionId != model.RegionId)
		{
			throw new InvalidOperationException("City does not belong to Region or does not exist.");
		}

		var exists = await _db.Users.AsNoTracking().AnyAsync(x => x.PhoneNumber == model.PhoneNumber, cancellationToken);
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
			Gender = model.Gender,
			PhoneNumber = model.PhoneNumber,
			PasswordHash = _passwordHasher.Hash(model.PasswordHash),
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

	public async Task<bool> ValidatePasswordAsync(string phoneNumber, string passwordHash, CancellationToken cancellationToken)
	{
		var u = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.PhoneNumber == phoneNumber, cancellationToken);
		if(u is null)
		{
			return false;
		}

		return _passwordHasher.Verify(u.PasswordHash, passwordHash);
	}

	public async Task<bool> ValidatePasswordAsync(Guid userId, string passwordHash, CancellationToken cancellationToken)
	{
		var u = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
		if(u is null)
		{
			return false;
		}

		return _passwordHasher.Verify(u.PasswordHash, passwordHash);
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

	public async Task<bool> ChangePasswordAsync(Guid userId, string oldPasswordHash, string newPasswordHash, CancellationToken cancellationToken)
	{
		var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
		if(u is null)
		{
			return false;
		}

		if(!_passwordHasher.Verify(u.PasswordHash, oldPasswordHash))
		{
			return false;
		}

		u.PasswordHash = _passwordHasher.Hash(newPasswordHash);
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

		if(!_passwordHasher.Verify(u.PasswordHash, model.PasswordHash))
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
		u.Gender = model.Gender;
		u.BirthDate = model.BirthDate;
		u.RegionId = model.RegionId;
		u.CityId = model.CityId;

		await _db.SaveChangesAsync(cancellationToken);
		return true;
	}

	public async Task<bool> ChangePhoneAsync(Guid userId, string passwordHash, string newPhoneNumber, CancellationToken cancellationToken)
	{
		var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
		if(u is null)
		{
			return false;
		}

		if(!_passwordHasher.Verify(u.PasswordHash, passwordHash))
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