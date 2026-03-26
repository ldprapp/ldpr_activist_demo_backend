namespace LdprActivistDemo.Application.Referrals;

/// <summary>
/// Репозиторий настройки сумм реферальных бонусов.
/// </summary>
public interface IReferralRewardSettingsRepository
{
	/// <summary>
	/// Возвращает количество баллов для пользователя, который пригласил нового участника.
	/// </summary>
	Task<int> GetInviterRewardPointsAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Возвращает количество баллов для нового пользователя, зарегистрированного по реферальному коду.
	/// </summary>
	Task<int> GetInvitedUserRewardPointsAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Сохраняет количество баллов для пользователя, который пригласил нового участника.
	/// </summary>
	Task SetInviterRewardPointsAsync(
		int inviterRewardPoints,
		CancellationToken cancellationToken);

	/// <summary>
	/// Сохраняет количество баллов для нового пользователя, зарегистрированного по реферальному коду.
	/// </summary>
	Task SetInvitedUserRewardPointsAsync(
		int invitedUserRewardPoints,
		CancellationToken cancellationToken);
}