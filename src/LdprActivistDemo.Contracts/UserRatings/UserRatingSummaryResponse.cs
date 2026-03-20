namespace LdprActivistDemo.Contracts.UserRatings;

public sealed record UserRatingSummaryResponse(
	Guid UserId,
	int? OverallRank,
	int? RegionRank,
	int? SettlementRank);