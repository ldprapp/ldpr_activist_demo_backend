namespace LdprActivistDemo.Application.UserRatings.Models;

public sealed record UserRatingSummaryModel(
	Guid UserId,
	int? OverallRank,
	int? RegionRank,
	int? SettlementRank);