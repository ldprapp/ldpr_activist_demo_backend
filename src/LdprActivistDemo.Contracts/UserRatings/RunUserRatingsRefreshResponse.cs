namespace LdprActivistDemo.Contracts.UserRatings;

public sealed record RunUserRatingsRefreshResponse(
	DateTimeOffset StartedAtUtc,
	DateTimeOffset CompletedAtUtc,
	int TotalUsers,
	int CreatedMissingRows,
	int UpdatedUsers);