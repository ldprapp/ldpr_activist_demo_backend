namespace LdprActivistDemo.Contracts.Users;

public sealed record UserDto(
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
	bool IsPhoneConfirmed)
{
	public string? AvatarImageUrl { get; init; }
}