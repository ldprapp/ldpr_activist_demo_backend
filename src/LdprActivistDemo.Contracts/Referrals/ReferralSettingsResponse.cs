namespace LdprActivistDemo.Contracts.Referrals;

/// <summary>
/// Текущие административные настройки реферальной системы.
/// </summary>
/// <param name="InviteTextTemplate">Текущий шаблон текста приглашения.</param>
/// <param name="InviterRewardPoints">Текущее количество баллов для приглашающего пользователя.</param>
/// <param name="InvitedUserRewardPoints">Текущее количество баллов для приглашённого пользователя.</param>
public sealed record ReferralSettingsResponse(
	string InviteTextTemplate,
	int InviterRewardPoints,
	int InvitedUserRewardPoints);