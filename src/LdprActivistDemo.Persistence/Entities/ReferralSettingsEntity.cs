namespace LdprActivistDemo.Persistence;

public sealed class ReferralSettingsEntity
{
	public int Id { get; set; }

	public string InviteTextTemplate { get; set; } = string.Empty;
}