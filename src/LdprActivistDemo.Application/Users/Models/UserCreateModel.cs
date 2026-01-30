namespace LdprActivistDemo.Application.Users.Models;

public sealed record UserCreateModel(
	string LastName,
	string FirstName,
	string? MiddleName,
	string? Gender,
	string PhoneNumber,
	string Password,
	DateOnly BirthDate,
	int RegionId,
	int CityId,
	Guid? AvatarImageId = null);