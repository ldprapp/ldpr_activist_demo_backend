namespace LdprActivistDemo.Application.Referrals;

/// <summary>
/// Значения по умолчанию для singleton-настроек реферальной системы.
/// </summary>
public static class ReferralSettingsDefaults
{
	/// <summary>
	/// Шаблон текста приглашения по умолчанию.
	/// </summary>
	public const string InviteTextTemplate =
"Присоединяйтесь к приложению «ЛДПР Активист». При регистрации укажите мой реферальный код {code} и получите {reward} баллов в подарок.";

	/// <summary>
	/// Количество баллов по умолчанию для пользователя, который пригласил нового участника.
	/// </summary>
	public const int InviterRewardPoints = 100;

	/// <summary>
	/// Количество баллов по умолчанию для нового пользователя, зарегистрировавшегося по реферальному коду.
	/// </summary>
	public const int InvitedUserRewardPoints = 100;
}