using LdprActivistDemo.Application.Diagnostics;
using LdprActivistDemo.Application.Logging;
using LdprActivistDemo.Application.UserRatings.Models;

using Microsoft.Extensions.Logging;

namespace LdprActivistDemo.Application.UserRatings;

public sealed class UserRatingsService : IUserRatingsService
{
	private readonly IUserRatingRepository _ratings;
	private readonly ILogger<UserRatingsService> _logger;

	public UserRatingsService(
		IUserRatingRepository ratings,
		ILogger<UserRatingsService> logger)
	{
		_ratings = ratings ?? throw new ArgumentNullException(nameof(ratings));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	public async Task<UserRatingsResult<IReadOnlyList<UserRatingFeedItemModel>>> GetFeedAsync(
		string? regionName,
		string? settlementName,
		int? start,
		int? end,
		CancellationToken cancellationToken)
	{
		var regionIsValid = TryNormalizeOptionalName(regionName, out var normalizedRegionName);
		var settlementIsValid = TryNormalizeOptionalName(settlementName, out var normalizedSettlementName);
		var scope = normalizedRegionName is null
			? "overall"
			: normalizedSettlementName is null
				? "region"
				: "settlement";

		var properties = new (string Name, object? Value)[]
		{
			("Scope", scope),
			("RegionName", normalizedRegionName),
			("SettlementName", normalizedSettlementName),
			("Start", start),
			("End", end),
		};

		using var logScope = _logger.BeginExecutionScope(
			DomainLogEvents.UserRatings.GetFeed,
			LogLayers.ApplicationService,
			ApplicationLogOperations.UserRatings.GetFeed,
			properties);

		_logger.LogStarted(
			DomainLogEvents.UserRatings.GetFeed,
			LogLayers.ApplicationService,
			ApplicationLogOperations.UserRatings.GetFeed,
			"User ratings feed read started.",
			properties);

		try
		{
			if(!regionIsValid
			   || !settlementIsValid
			   || (normalizedSettlementName is not null && normalizedRegionName is null)
			   || !IsPaginationValid(start, end))
			{
				_logger.LogRejected(
					LogLevel.Warning,
					DomainLogEvents.UserRatings.GetFeed,
					LogLayers.ApplicationService,
					ApplicationLogOperations.UserRatings.GetFeed,
					"User ratings feed read rejected by validation.",
					StructuredLog.Combine(properties, ("Error", UserRatingsError.ValidationFailed)));

				return UserRatingsResult<IReadOnlyList<UserRatingFeedItemModel>>.Fail(UserRatingsError.ValidationFailed);
			}

			var list = await _ratings.GetFeedAsync(
				normalizedRegionName,
				normalizedSettlementName,
				start,
				end,
				cancellationToken);

			_logger.LogCompleted(
				LogLevel.Debug,
				DomainLogEvents.UserRatings.GetFeed,
				LogLayers.ApplicationService,
				ApplicationLogOperations.UserRatings.GetFeed,
				"User ratings feed read completed.",
				StructuredLog.Combine(properties, ("Count", list.Count)));

			return UserRatingsResult<IReadOnlyList<UserRatingFeedItemModel>>.Ok(list);
		}
		catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
		{
			_logger.LogAborted(
				LogLevel.Information,
				DomainLogEvents.UserRatings.GetFeed,
				LogLayers.ApplicationService,
				ApplicationLogOperations.UserRatings.GetFeed,
				"User ratings feed read aborted.",
				properties);
			throw;
		}
		catch(Exception ex)
		{
			_logger.LogFailed(
				LogLevel.Error,
				DomainLogEvents.UserRatings.GetFeed,
				LogLayers.ApplicationService,
				ApplicationLogOperations.UserRatings.GetFeed,
				"User ratings feed read failed.",
				ex,
				properties);
			throw;
		}
	}

	public async Task<UserRatingsResult<UserRatingSummaryModel>> GetUserRanksAsync(
		Guid userId,
		CancellationToken cancellationToken)
	{
		var properties = new (string Name, object? Value)[]
		{
			("UserId", userId),
		};

		using var logScope = _logger.BeginExecutionScope(
			DomainLogEvents.UserRatings.GetUserRanks,
			LogLayers.ApplicationService,
			ApplicationLogOperations.UserRatings.GetUserRanks,
			properties);

		_logger.LogStarted(
			DomainLogEvents.UserRatings.GetUserRanks,
			LogLayers.ApplicationService,
			ApplicationLogOperations.UserRatings.GetUserRanks,
			"User ratings summary read started.",
			properties);

		try
		{
			if(userId == Guid.Empty)
			{
				_logger.LogRejected(
					LogLevel.Warning,
					DomainLogEvents.UserRatings.GetUserRanks,
					LogLayers.ApplicationService,
					ApplicationLogOperations.UserRatings.GetUserRanks,
					"User ratings summary read rejected by validation.",
					StructuredLog.Combine(properties, ("Error", UserRatingsError.ValidationFailed)));

				return UserRatingsResult<UserRatingSummaryModel>.Fail(UserRatingsError.ValidationFailed);
			}

			var summary = await _ratings.GetUserRanksAsync(userId, cancellationToken);
			if(summary is null)
			{
				_logger.LogRejected(
					LogLevel.Warning,
					DomainLogEvents.UserRatings.GetUserRanks,
					LogLayers.ApplicationService,
					ApplicationLogOperations.UserRatings.GetUserRanks,
					"User ratings summary read rejected. User not found.",
					StructuredLog.Combine(properties, ("Error", UserRatingsError.UserNotFound)));

				return UserRatingsResult<UserRatingSummaryModel>.Fail(UserRatingsError.UserNotFound);
			}

			_logger.LogCompleted(
				LogLevel.Debug,
				DomainLogEvents.UserRatings.GetUserRanks,
				LogLayers.ApplicationService,
				ApplicationLogOperations.UserRatings.GetUserRanks,
				"User ratings summary read completed.",
				properties);

			return UserRatingsResult<UserRatingSummaryModel>.Ok(summary);
		}
		catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
		{
			_logger.LogAborted(
				LogLevel.Information,
				DomainLogEvents.UserRatings.GetUserRanks,
				LogLayers.ApplicationService,
				ApplicationLogOperations.UserRatings.GetUserRanks,
				"User ratings summary read aborted.",
				properties);
			throw;
		}
		catch(Exception ex)
		{
			_logger.LogFailed(
				LogLevel.Error,
				DomainLogEvents.UserRatings.GetUserRanks,
				LogLayers.ApplicationService,
				ApplicationLogOperations.UserRatings.GetUserRanks,
				"User ratings summary read failed.",
				ex,
				properties);
			throw;
		}
	}

	private static bool TryNormalizeOptionalName(string? raw, out string? normalized)
	{
		if(raw is null)
		{
			normalized = null;
			return true;
		}

		normalized = raw.Trim();
		if(normalized.Length == 0)
		{
			normalized = null;
			return false;
		}

		return true;
	}

	private static bool IsPaginationValid(int? start, int? end)
	{
		if(start is null && end is null)
		{
			return true;
		}

		if(start is null || end is null)
		{
			return false;
		}

		return start.Value > 0
			&& end.Value > 0
			&& end.Value >= start.Value;
	}
}