namespace LdprActivistDemo.Application.Users.Models;

public sealed record UserFullNameModel(
	Guid Id,
	string LastName,
	string FirstName,
	string? MiddleName);