using LdprActivistDemo.Application.Users.Models;

namespace LdprActivistDemo.Persistence;

public sealed class User
{
	public Guid Id { get; set; }

	public string LastName { get; set; } = string.Empty;

	public string FirstName { get; set; } = string.Empty;

	public string? MiddleName { get; set; }

	public string? Gender { get; set; }

	public string PhoneNumber { get; set; } = string.Empty;

	public string PasswordHash { get; set; } = string.Empty;

	public DateOnly BirthDate { get; set; }

	public int RegionId { get; set; }

	public Region Region { get; set; } = null!;

	public int CityId { get; set; }

	public City City { get; set; } = null!;

	public string Role { get; set; } = UserRoles.Activist;

	public bool IsPhoneConfirmed { get; set; }

	public string? AvatarImageUrl { get; set; }
}