namespace LdprActivistDemo.Application.Referrals;

public interface IReferralSettingsRepository
{
	Task<string> GetInviteTextTemplateAsync(CancellationToken cancellationToken);
	Task<int> GetInviterRewardPointsAsync(CancellationToken cancellationToken);
	Task<int> GetInvitedUserRewardPointsAsync(CancellationToken cancellationToken);

	Task SetInviteTextTemplateAsync(string inviteTextTemplate, CancellationToken cancellationToken);

	Task SetSettingsAsync(
		string inviteTextTemplate,
		int inviterRewardPoints,
		int invitedUserRewardPoints,
		CancellationToken cancellationToken);
}