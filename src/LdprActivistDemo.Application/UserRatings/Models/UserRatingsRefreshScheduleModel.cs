namespace LdprActivistDemo.Application.UserRatings.Models;

/// <summary>
/// Текущее расписание ежедневного пересчёта пользовательских рейтингов.
/// </summary>
public sealed record UserRatingsRefreshScheduleModel(
	string JobName,
	int Hour,
	int Minute,
	DateOnly? LastCompletedLocalDate,
	DateTimeOffset? LastCompletedAtUtc);