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
	int Points)
{
	/// <summary>Идентификатор аватарки пользователя (Guid в строке) или <c>null</c>, если аватарки нет.</summary>
	/// <remarks>Получение бинарных данных: <c>GET /api/v1/images/{id}</c>.</remarks>
	public string? AvatarImageUrl { get; init; }
}