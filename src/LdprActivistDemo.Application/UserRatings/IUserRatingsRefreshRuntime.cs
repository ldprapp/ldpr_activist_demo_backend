namespace LdprActivistDemo.Application.UserRatings;

/// <summary>
/// Runtime-компонент, который хранит и исполняет расписание фонового пересчёта рейтингов.
/// </summary>
public interface IUserRatingsRefreshRuntime
{
	/// <summary>
	/// Возвращает текущее расписание пересчёта рейтингов.
	/// </summary>
	Task<LdprActivistDemo.Application.UserRatings.Models.UserRatingsRefreshScheduleModel> GetScheduleAsync(
		CancellationToken cancellationToken);

	/// <summary>
	/// Обновляет текущее расписание пересчёта рейтингов.
	/// </summary>
	Task<LdprActivistDemo.Application.UserRatings.Models.UserRatingsRefreshScheduleModel> SetScheduleAsync(
		int hour,
		int minute,
		CancellationToken cancellationToken);

	/// <summary>
	/// Принудительно запускает пересчёт рейтингов немедленно.
	/// </summary>
	Task<LdprActivistDemo.Application.UserRatings.Models.UserRatingsRefreshRunModel> RunNowAsync(
		CancellationToken cancellationToken);
}