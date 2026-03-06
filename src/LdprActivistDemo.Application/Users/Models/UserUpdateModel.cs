namespace LdprActivistDemo.Application.Users.Models;

public sealed record UserUpdateModel(
	Guid UserId,
	string LastName,
	string FirstName,
	string? MiddleName,
	string? Gender,
	DateOnly BirthDate,
	string RegionName,
	string CityName,
	Guid? AvatarImageId = null);