namespace LdprActivistDemo.Application.Users.Models;

public sealed record UserPublicModel(
	Guid Id,
	string LastName,
	string FirstName,
	string? MiddleName,
	string? Gender,
	string PhoneNumber,
	DateOnly BirthDate,
	int RegionId,
	int CityId,
	bool IsPhoneConfirmed,
	int Points);