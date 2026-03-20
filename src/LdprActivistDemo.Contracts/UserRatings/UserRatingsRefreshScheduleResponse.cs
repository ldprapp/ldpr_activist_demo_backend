namespace LdprActivistDemo.Contracts.UserRatings;

public sealed record UserRatingsRefreshScheduleResponse(
	int Hour,
	int Minute,
	string LocalTime,
	string Locale,
	DateOnly? LastCompletedLocalDate,
	DateTimeOffset? LastCompletedAtUtc);