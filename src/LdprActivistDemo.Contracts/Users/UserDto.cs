namespace LdprActivistDemo.Contracts.Users;

public sealed record UserDto(
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