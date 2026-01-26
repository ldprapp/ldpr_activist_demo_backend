namespace LdprActivistDemo.Application.Users.Models;

public sealed record UserUpdateModel(
	Guid UserId,
	string LastName,
	string FirstName,
	string? MiddleName,
	string? Gender,
	DateOnly BirthDate,
	int RegionId,
	int CityId);