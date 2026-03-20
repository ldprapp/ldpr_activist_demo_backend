namespace LdprActivistDemo.Application.UserRatings;

/// <summary>
/// Административные операции управления расписанием и ручным запуском пересчёта рейтингов.
/// </summary>
public interface IUserRatingsRefreshAdminService
{
	/// <summary>
	/// Возвращает текущее расписание пересчёта рейтингов для администратора.
	/// </summary>
	Task<LdprActivistDemo.Application.UserRatings.Models.UserRatingsAdminResult<
		LdprActivistDemo.Application.UserRatings.Models.UserRatingsRefreshScheduleModel>> GetRefreshScheduleAsync(
		Guid actorUserId,
		string actorUserPassword,
		CancellationToken cancellationToken);

	/// <summary>
	/// Обновляет текущее расписание пересчёта рейтингов для администратора.
	/// </summary>
	Task<LdprActivistDemo.Application.UserRatings.Models.UserRatingsAdminResult<
		LdprActivistDemo.Application.UserRatings.Models.UserRatingsRefreshScheduleModel>> UpdateRefreshScheduleAsync(
		Guid actorUserId,
		string actorUserPassword,
		int hour,
		int minute,
		CancellationToken cancellationToken);

	/// <summary>
	/// Выполняет принудительный ручной запуск пересчёта рейтингов для администратора.
	/// </summary>
	Task<LdprActivistDemo.Application.UserRatings.Models.UserRatingsAdminResult<
		LdprActivistDemo.Application.UserRatings.Models.UserRatingsRefreshRunModel>> RunRefreshNowAsync(
		Guid actorUserId,
		string actorUserPassword,
		CancellationToken cancellationToken);
}