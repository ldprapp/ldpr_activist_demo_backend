namespace LdprActivistDemo.Contracts.Tasks;

public sealed record SubmittedUserDto(
	Guid Id,
	string LastName,
	string FirstName,
	string? MiddleName);