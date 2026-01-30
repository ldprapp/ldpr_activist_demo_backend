namespace LdprActivistDemo.Contracts.Users;

public sealed record UpdateUserRequest(
	string LastName,
	string FirstName,
	string? MiddleName,
	string? Gender,
	DateOnly BirthDate,
	int RegionId,
	int CityId,
	Guid? AvatarImageId = null);