using LdprActivistDemo.Application.Referrals.Models;

namespace LdprActivistDemo.Application.Referrals;

public interface IReferralService
{
	Task<ReferralContentResult> GetContentAsync(
		Guid actorUserId,
		string actorUserPassword,
		Guid userId,
		ReferralContentFormat format,
		CancellationToken cancellationToken);

	Task<ReferralSettingsReadResult> GetSettingsAsync(
		CancellationToken cancellationToken);

	Task<ReferralInvitedUsersReadResult> GetInvitedUsersAsync(
		Guid actorUserId,
		string actorUserPassword,
		Guid userId,
		CancellationToken cancellationToken);

	Task<ReferralSettingsUpdateResult> UpdateSettingsAsync(
		Guid actorUserId,
		string actorUserPassword,
		string inviteTextTemplate,
		int inviterRewardPoints,
		int invitedUserRewardPoints,
		CancellationToken cancellationToken);

	Task<ReferralTemplateUpdateResult> SetInviteTextTemplateAsync(
		Guid actorUserId,
		string actorUserPassword,
		string inviteTextTemplate,
		CancellationToken cancellationToken);
}