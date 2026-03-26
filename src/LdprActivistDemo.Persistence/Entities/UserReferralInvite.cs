namespace LdprActivistDemo.Persistence;

public sealed class UserReferralInvite
{
	public Guid InviterUserId { get; set; }

	public User InviterUser { get; set; } = null!;

	public Guid InvitedUserId { get; set; }

	public User InvitedUser { get; set; } = null!;
}