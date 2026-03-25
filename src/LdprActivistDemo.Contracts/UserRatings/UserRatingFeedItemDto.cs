namespace LdprActivistDemo.Contracts.UserRatings;

public sealed record UserRatingFeedItemDto(
	Guid Id,
	string LastName,
	string FirstName,
	string? MiddleName,
	string? Gender,
	string PhoneNumber,
	DateOnly BirthDate,
	string RegionName,
	string SettlementName,
	string Role,
	bool IsPhoneConfirmed,
	int? Rank)
{
	public string? AvatarImageUrl { get; init; }
	public int Balance { get; init; }
}