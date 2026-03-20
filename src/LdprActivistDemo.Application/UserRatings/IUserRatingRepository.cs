using LdprActivistDemo.Application.UserRatings.Models;

namespace LdprActivistDemo.Application.UserRatings;

/// <summary>
/// Репозиторий для хранения пользовательских мест в рейтинге и состояния фонового пересчёта.
/// </summary>
public interface IUserRatingRepository
{
	/// <summary>
	/// Возвращает локальную дату расписания, для которой пересчёт уже был успешно завершён.
	/// </summary>
	Task<DateOnly?> GetLastCompletedLocalDateAsync(
		string jobName,
		CancellationToken cancellationToken);

	/// <summary>
	/// Сохраняет факт успешного завершения пересчёта для указанной локальной даты расписания.
	/// </summary>
	Task SetLastCompletedLocalDateAsync(
		string jobName,
		DateOnly localDate,
		DateTimeOffset completedAtUtc,
		CancellationToken cancellationToken);

	/// <summary>
	/// Возвращает текущее расписание ежедневного пересчёта рейтинга.
	/// </summary>
	Task<UserRatingsRefreshScheduleModel> GetScheduleAsync(
		string jobName,
		int defaultHour,
		int defaultMinute,
		CancellationToken cancellationToken);

	/// <summary>
	/// Создаёт или обновляет расписание ежедневного пересчёта рейтинга.
	/// </summary>
	Task<UserRatingsRefreshScheduleModel> SetScheduleAsync(
		string jobName,
		int hour,
		int minute,
		CancellationToken cancellationToken);

	/// <summary>
	/// Возвращает ленту пользователей, отсортированную по соответствующему рангу
	/// (общему, региональному или по населённому пункту).
	/// </summary>
	Task<IReadOnlyList<UserRatingFeedItemModel>> GetFeedAsync(
		string? regionName,
		string? settlementName,
		int? start,
		int? end,
		CancellationToken cancellationToken);

	/// <summary>
	/// Возвращает все три пользовательских места в рейтинге.
	/// </summary>
	Task<UserRatingSummaryModel?> GetUserRanksAsync(
		Guid userId,
		CancellationToken cancellationToken);

	/// <summary>
	/// Пересчитывает все пользовательские места в рейтинге.
	/// </summary>
	Task<UserRatingsRefreshResult> RecalculateRanksAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Результат пересчёта пользовательских мест в рейтинге.
/// </summary>
/// <param name="TotalUsers">Общее количество пользователей, учтённых в пересчёте.</param>
/// <param name="CreatedMissingRows">Количество автоматически дозаполненных строк в таблице рейтингов.</param>
/// <param name="UpdatedUsers">Количество обновлённых строк рейтинга.</param>
public sealed record UserRatingsRefreshResult(
	int TotalUsers,
	int CreatedMissingRows,
	int UpdatedUsers);