namespace LdprActivistDemo.Application.Users.Models;

public sealed record UserInternalModel(
	Guid Id,
	string LastName,
	string FirstName,
	string? MiddleName,
	string? Gender,
	string PhoneNumber,
	string PasswordHash,
	DateOnly BirthDate,
	string RegionName,
	string SettlementName,
	string Role,
	bool IsPhoneConfirmed,
	string? AvatarImageUrl);