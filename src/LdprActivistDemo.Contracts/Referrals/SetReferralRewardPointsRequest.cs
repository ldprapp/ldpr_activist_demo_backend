namespace LdprActivistDemo.Contracts.Referrals;

/// <summary>
/// Тело запроса на изменение количества реферальных баллов.
/// </summary>
public sealed record SetReferralRewardPointsRequest(int Points);