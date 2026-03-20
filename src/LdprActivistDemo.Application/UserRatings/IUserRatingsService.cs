using LdprActivistDemo.Application.UserRatings.Models;

namespace LdprActivistDemo.Application.UserRatings;

public interface IUserRatingsService
{
	Task<UserRatingsResult<IReadOnlyList<UserRatingFeedItemModel>>> GetFeedAsync(
		string? regionName,
		string? settlementName,
		int? start,
		int? end,
		CancellationToken cancellationToken);

	Task<UserRatingsResult<UserRatingSummaryModel>> GetUserRanksAsync(
		Guid userId,
		CancellationToken cancellationToken);
}