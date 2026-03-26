namespace LdprActivistDemo.Contracts.Referrals;

/// <summary>
/// Тело запроса на атомарное обновление всех административных настроек реферальной системы.
/// </summary>
public sealed record UpdateReferralSettingsRequest(
	string InviteTextTemplate,
	int InviterRewardPoints,
	int InvitedUserRewardPoints);