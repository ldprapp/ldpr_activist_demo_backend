using LdprActivistDemo.Application.Diagnostics;
using LdprActivistDemo.Application.Logging;
using LdprActivistDemo.Application.UserRatings;
using LdprActivistDemo.Application.UserRatings.Models;
using LdprActivistDemo.Persistence.Logging;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LdprActivistDemo.Persistence;

public sealed class UserRatingRepository : IUserRatingRepository
{
	private const int DefaultScheduledHour = 4;
	private const int DefaultScheduledMinute = 0;

	private readonly AppDbContext _db;
	private readonly ILogger<UserRatingRepository> _logger;

	public UserRatingRepository(
		AppDbContext db,
		ILogger<UserRatingRepository> logger)
	{
		_db = db ?? throw new ArgumentNullException(nameof(db));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	public async Task<UserRatingsRefreshScheduleModel> GetScheduleAsync(
		string jobName,
		int defaultHour,
		int defaultMinute,
		CancellationToken cancellationToken)
	{
		if(string.IsNullOrWhiteSpace(jobName))
		{
			throw new ArgumentException("Job name must be non-empty.", nameof(jobName));
		}

		var state = await _db.UserRatingsRefreshStates
			.AsNoTracking()
			.FirstOrDefaultAsync(x => x.JobName == jobName, cancellationToken);

		return new UserRatingsRefreshScheduleModel(
			jobName,
			state?.ScheduledHour ?? defaultHour,
			state?.ScheduledMinute ?? defaultMinute,
			state?.LastCompletedLocalDate,
			state?.LastCompletedAtUtc);
	}

	public async Task<UserRatingsRefreshScheduleModel> SetScheduleAsync(
		string jobName,
		int hour,
		int minute,
		CancellationToken cancellationToken)
	{
		if(string.IsNullOrWhiteSpace(jobName))
		{
			throw new ArgumentException("Job name must be non-empty.", nameof(jobName));
		}

		var state = await _db.UserRatingsRefreshStates
			.FirstOrDefaultAsync(x => x.JobName == jobName, cancellationToken);

		if(state is null)
		{
			state = new UserRatingsRefreshState
			{
				JobName = jobName,
			};

			_db.UserRatingsRefreshStates.Add(state);
		}

		state.ScheduledHour = hour;
		state.ScheduledMinute = minute;

		await _db.SaveChangesAsync(cancellationToken);

		return new UserRatingsRefreshScheduleModel(
			state.JobName,
			state.ScheduledHour,
			state.ScheduledMinute,
			state.LastCompletedLocalDate,
			state.LastCompletedAtUtc);
	}

	public async Task<DateOnly?> GetLastCompletedLocalDateAsync(
		string jobName,
		CancellationToken cancellationToken)
	{
		if(string.IsNullOrWhiteSpace(jobName))
		{
			return null;
		}

		return await _db.UserRatingsRefreshStates
			.AsNoTracking()
			.Where(x => x.JobName == jobName)
			.Select(x => x.LastCompletedLocalDate)
			.FirstOrDefaultAsync(cancellationToken);
	}

	public async Task SetLastCompletedLocalDateAsync(
		string jobName,
		DateOnly localDate,
		DateTimeOffset completedAtUtc,
		CancellationToken cancellationToken)
	{
		if(string.IsNullOrWhiteSpace(jobName))
		{
			throw new ArgumentException("Job name must be non-empty.", nameof(jobName));
		}

		var state = await _db.UserRatingsRefreshStates
			.FirstOrDefaultAsync(x => x.JobName == jobName, cancellationToken);

		if(state is null)
		{
			state = new UserRatingsRefreshState
			{
				JobName = jobName,
				ScheduledHour = DefaultScheduledHour,
				ScheduledMinute = DefaultScheduledMinute,
			};

			_db.UserRatingsRefreshStates.Add(state);
		}

		state.LastCompletedLocalDate = localDate;
		state.LastCompletedAtUtc = completedAtUtc;

		await _db.SaveChangesAsync(cancellationToken);
	}

	public async Task<IReadOnlyList<UserRatingFeedItemModel>> GetFeedAsync(
		string? regionName,
		string? settlementName,
		int? start,
		int? end,
		CancellationToken cancellationToken)
	{
		var normalizedRegionName = NormalizeOptionalName(regionName);
		var normalizedSettlementName = NormalizeOptionalName(settlementName);
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
			DomainLogEvents.UserRatings.Repository.GetFeed,
			LogLayers.PersistenceRepository,
			PersistenceLogOperations.UserRatings.GetFeed,
			properties);

		_logger.LogStarted(
			DomainLogEvents.UserRatings.Repository.GetFeed,
			LogLayers.PersistenceRepository,
			PersistenceLogOperations.UserRatings.GetFeed,
			"User ratings feed query started.",
			properties);

		try
		{
			var baseQuery =
				from u in _db.Users.AsNoTracking()
				join ur in _db.UserRatings.AsNoTracking() on u.Id equals ur.UserId into ratingGroup
				from ur in ratingGroup.DefaultIfEmpty()
				select new
				{
					User = u,
					OverallRank = ur == null ? (int?)null : ur.OverallRank,
					RegionRank = ur == null ? (int?)null : ur.RegionRank,
					SettlementRank = ur == null ? (int?)null : ur.SettlementRank,
				};

			if(normalizedRegionName is not null)
			{
				var regionKey = normalizedRegionName.ToLowerInvariant();
				baseQuery = baseQuery.Where(x => x.User.Region.Name.ToLower() == regionKey);
			}

			if(normalizedSettlementName is not null)
			{
				var settlementKey = normalizedSettlementName.ToLowerInvariant();
				baseQuery = baseQuery.Where(x => x.User.Settlement.Name.ToLower() == settlementKey);
			}

			IQueryable<UserRatingFeedItemModel> projectedQuery;

			if(normalizedRegionName is not null && normalizedSettlementName is not null)
			{
				projectedQuery = baseQuery
					.OrderBy(x => x.SettlementRank)
					.ThenBy(x => x.User.Id)
					.Select(x => new UserRatingFeedItemModel(
						x.User.Id,
						x.User.LastName,
						x.User.FirstName,
						x.User.MiddleName,
						x.User.Gender,
						x.User.PhoneNumber,
						x.User.BirthDate,
						x.User.Region.Name,
						x.User.Settlement.Name,
						x.User.Role,
						x.User.IsPhoneConfirmed,
						x.User.AvatarImageUrl,
						x.SettlementRank));
			}
			else if(normalizedRegionName is not null)
			{
				projectedQuery = baseQuery
					.OrderBy(x => x.RegionRank)
					.ThenBy(x => x.User.Id)
					.Select(x => new UserRatingFeedItemModel(
						x.User.Id,
						x.User.LastName,
						x.User.FirstName,
						x.User.MiddleName,
						x.User.Gender,
						x.User.PhoneNumber,
						x.User.BirthDate,
						x.User.Region.Name,
						x.User.Settlement.Name,
						x.User.Role,
						x.User.IsPhoneConfirmed,
						x.User.AvatarImageUrl,
						x.RegionRank));
			}
			else
			{
				projectedQuery = baseQuery
					.OrderBy(x => x.OverallRank)
					.ThenBy(x => x.User.Id)
					.Select(x => new UserRatingFeedItemModel(
						x.User.Id,
						x.User.LastName,
						x.User.FirstName,
						x.User.MiddleName,
						x.User.Gender,
						x.User.PhoneNumber,
						x.User.BirthDate,
						x.User.Region.Name,
						x.User.Settlement.Name,
						x.User.Role,
						x.User.IsPhoneConfirmed,
						x.User.AvatarImageUrl,
						x.OverallRank));
			}

			if(start is not null && end is not null)
			{
				var skip = Math.Max(0, start.Value - 1);
				var take = end.Value - start.Value + 1;
				if(take <= 0)
				{
					return Array.Empty<UserRatingFeedItemModel>();
				}

				projectedQuery = projectedQuery
					.Skip(skip)
					.Take(take);
			}

			var list = await projectedQuery.ToListAsync(cancellationToken);

			_logger.LogCompleted(
				LogLevel.Debug,
				DomainLogEvents.UserRatings.Repository.GetFeed,
				LogLayers.PersistenceRepository,
				PersistenceLogOperations.UserRatings.GetFeed,
				"User ratings feed query completed.",
				StructuredLog.Combine(properties, ("Count", list.Count)));

			return list;
		}
		catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
		{
			_logger.LogAborted(
				LogLevel.Information,
				DomainLogEvents.UserRatings.Repository.GetFeed,
				LogLayers.PersistenceRepository,
				PersistenceLogOperations.UserRatings.GetFeed,
				"User ratings feed query aborted.",
				properties);
			throw;
		}
		catch(Exception ex)
		{
			_logger.LogFailed(
				LogLevel.Error,
				DomainLogEvents.UserRatings.Repository.GetFeed,
				LogLayers.PersistenceRepository,
				PersistenceLogOperations.UserRatings.GetFeed,
				"User ratings feed query failed.",
				ex,
				properties);
			throw;
		}
	}

	public async Task<UserRatingSummaryModel?> GetUserRanksAsync(
		Guid userId,
		CancellationToken cancellationToken)
	{
		var properties = new (string Name, object? Value)[]
		{
			("UserId", userId),
		};

		using var logScope = _logger.BeginExecutionScope(
			DomainLogEvents.UserRatings.Repository.GetUserRanks,
			LogLayers.PersistenceRepository,
			PersistenceLogOperations.UserRatings.GetUserRanks,
			properties);

		_logger.LogStarted(
			DomainLogEvents.UserRatings.Repository.GetUserRanks,
			LogLayers.PersistenceRepository,
			PersistenceLogOperations.UserRatings.GetUserRanks,
			"User ratings summary query started.",
			properties);

		try
		{
			var summary = await (
				from u in _db.Users.AsNoTracking()
				join ur in _db.UserRatings.AsNoTracking() on u.Id equals ur.UserId into ratingGroup
				from ur in ratingGroup.DefaultIfEmpty()
				where u.Id == userId
				select new UserRatingSummaryModel(
					u.Id,
					ur == null ? (int?)null : ur.OverallRank,
					ur == null ? (int?)null : ur.RegionRank,
					ur == null ? (int?)null : ur.SettlementRank))
				.FirstOrDefaultAsync(cancellationToken);

			_logger.LogCompleted(
				LogLevel.Debug,
				DomainLogEvents.UserRatings.Repository.GetUserRanks,
				LogLayers.PersistenceRepository,
				PersistenceLogOperations.UserRatings.GetUserRanks,
				"User ratings summary query completed.",
				StructuredLog.Combine(properties, ("Found", summary is not null)));

			return summary;
		}
		catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
		{
			_logger.LogAborted(
				LogLevel.Information,
				DomainLogEvents.UserRatings.Repository.GetUserRanks,
				LogLayers.PersistenceRepository,
				PersistenceLogOperations.UserRatings.GetUserRanks,
				"User ratings summary query aborted.",
				properties);
			throw;
		}
		catch(Exception ex)
		{
			_logger.LogFailed(
				LogLevel.Error,
				DomainLogEvents.UserRatings.Repository.GetUserRanks,
				LogLayers.PersistenceRepository,
				PersistenceLogOperations.UserRatings.GetUserRanks,
				"User ratings summary query failed.",
				ex,
				properties);
			throw;
		}
	}

	public async Task<UserRatingsRefreshResult> RecalculateRanksAsync(CancellationToken cancellationToken)
	{
		const string insertMissingRowsSql = """
			INSERT INTO user_ratings ("UserId", "OverallRank", "RegionRank", "SettlementRank")
			SELECT u."Id", NULL, NULL, NULL
			FROM users u
			LEFT JOIN user_ratings ur ON ur."UserId" = u."Id"
			WHERE ur."UserId" IS NULL;
			""";

		const string updateRanksSql = """
			WITH points AS (
				SELECT
					u."Id" AS "UserId",
					u."RegionId" AS "RegionId",
					u."SettlementId" AS "SettlementId",
					COALESCE(SUM(t."Amount"), 0) AS "TotalPoints"
				FROM users u
				LEFT JOIN user_points_transactions t ON t."UserId" = u."Id"
				GROUP BY u."Id", u."RegionId", u."SettlementId"
			),
			ranks AS (
				SELECT
					p."UserId" AS "UserId",
					DENSE_RANK() OVER (ORDER BY p."TotalPoints" DESC) AS "OverallRank",
					DENSE_RANK() OVER (PARTITION BY p."RegionId" ORDER BY p."TotalPoints" DESC) AS "RegionRank",
					DENSE_RANK() OVER (PARTITION BY p."SettlementId" ORDER BY p."TotalPoints" DESC) AS "SettlementRank"
				FROM points p
			)
			UPDATE user_ratings ur
			SET
				"OverallRank" = r."OverallRank",
				"RegionRank" = r."RegionRank",
				"SettlementRank" = r."SettlementRank"
			FROM ranks r
			WHERE ur."UserId" = r."UserId";
			""";

		await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
		var createdMissingRows = await _db.Database.ExecuteSqlRawAsync(insertMissingRowsSql, cancellationToken);
		var updatedUsers = await _db.Database.ExecuteSqlRawAsync(updateRanksSql, cancellationToken);
		var totalUsers = await _db.Users.AsNoTracking().CountAsync(cancellationToken);
		await transaction.CommitAsync(cancellationToken);

		return new UserRatingsRefreshResult(totalUsers, createdMissingRows, updatedUsers);
	}

	private static string? NormalizeOptionalName(string? value)
	{
		var normalized = (value ?? string.Empty).Trim();
		return normalized.Length == 0 ? null : normalized;
	}
}