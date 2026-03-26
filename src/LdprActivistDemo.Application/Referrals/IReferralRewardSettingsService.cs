namespace LdprActivistDemo.Application.Referrals;

/// <summary>
/// Сервис административного изменения настроек реферальных бонусов.
/// </summary>
public interface IReferralRewardSettingsService
{
	/// <summary>
	/// Устанавливает количество баллов для приглашающего пользователя.
	/// </summary>
	Task<ReferralRewardSettingsChangeResult> SetInviterRewardPointsAsync(
		Guid actorUserId,
		string actorUserPassword,
		int points,
		CancellationToken cancellationToken);

	/// <summary>
	/// Устанавливает количество баллов для приглашённого пользователя.
	/// </summary>
	Task<ReferralRewardSettingsChangeResult> SetInvitedUserRewardPointsAsync(
		Guid actorUserId,
		string actorUserPassword,
		int points,
		CancellationToken cancellationToken);
}

/// <summary>
/// Ошибки изменения настроек реферальных бонусов.
/// </summary>
public enum ReferralRewardSettingsChangeError
{
	None = 0,
	ValidationFailed = 1,
	InvalidCredentials = 2,
	Forbidden = 3,
}

/// <summary>
/// Результат изменения настроек реферальных бонусов.
/// </summary>
public readonly record struct ReferralRewardSettingsChangeResult(ReferralRewardSettingsChangeError Error)
{
	public bool IsSuccess => Error == ReferralRewardSettingsChangeError.None;

	public static ReferralRewardSettingsChangeResult Success() => new(ReferralRewardSettingsChangeError.None);
	public static ReferralRewardSettingsChangeResult Fail(ReferralRewardSettingsChangeError error) => new(error);
}