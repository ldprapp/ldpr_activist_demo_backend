namespace LdprActivistDemo.Persistence;

public sealed class UserRating
{
	public Guid UserId { get; set; }

	public User User { get; set; } = null!;

	public int? OverallRank { get; set; }

	public int? RegionRank { get; set; }

	public int? SettlementRank { get; set; }
}