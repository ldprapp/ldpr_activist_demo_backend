namespace LdprActivistDemo.Contracts.Users;

public sealed record UserRegisterRequest(
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