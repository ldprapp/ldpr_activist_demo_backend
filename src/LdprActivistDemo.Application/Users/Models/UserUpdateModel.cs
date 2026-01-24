namespace LdprActivistDemo.Application.Users.Models;

public sealed record UserUpdateModel(
	Guid UserId,
	string LastName,
	string FirstName,
	string? MiddleName,
	string? Gender,
	string PasswordHash,
	DateOnly BirthDate,
	int RegionId,
	int CityId);