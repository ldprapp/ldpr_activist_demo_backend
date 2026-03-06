namespace LdprActivistDemo.Application.Users.Models;

public sealed record UserPublicModel(
	Guid Id,
	string LastName,
	string FirstName,
	string? MiddleName,
	string? Gender,
	string PhoneNumber,
	DateOnly BirthDate,
	string RegionName,
	string CityName,
	string Role,
	bool IsPhoneConfirmed,
	string? AvatarImageUrl);