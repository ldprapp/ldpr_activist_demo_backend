namespace LdprActivistDemo.Contracts.Users;

public sealed record UpdateUserRequest(
	string LastName,
	string FirstName,
	string? MiddleName,
	string? Gender,
	DateOnly BirthDate,
	string RegionName,
	string SettlementName,
	Guid? AvatarImageId = null);