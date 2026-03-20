namespace LdprActivistDemo.Application.UserRatings.Models;

/// <summary>
/// Результат ручного или фонового запуска пересчёта пользовательских рейтингов.
/// </summary>
public sealed record UserRatingsRefreshRunModel(
	DateTimeOffset StartedAtUtc,
	DateTimeOffset CompletedAtUtc,
	int TotalUsers,
	int CreatedMissingRows,
	int UpdatedUsers);