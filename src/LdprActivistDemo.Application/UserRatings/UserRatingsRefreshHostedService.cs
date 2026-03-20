using LdprActivistDemo.Application.Diagnostics;
using LdprActivistDemo.Application.Logging;
using LdprActivistDemo.Application.UserRatings.Models;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LdprActivistDemo.Application.UserRatings;

/// <summary>
/// Фоновая служба, которая ежедневно пересчитывает пользовательские места в рейтинге
/// по московскому времени, независимо от локального времени хоста, docker-контейнера
/// или сервера. Само расписание хранится в базе данных.
/// </summary>
public sealed class UserRatingsRefreshHostedService : BackgroundService, IUserRatingsRefreshRuntime
{
	private const string JobName = "user_ratings.daily_refresh";
	private const int DefaultScheduledHour = 4;
	private const int DefaultScheduledMinute = 0;
	private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);
	private static readonly TimeZoneInfo MoscowTimeZone = ResolveMoscowTimeZone();

	private readonly IServiceScopeFactory _scopeFactory;
	private readonly ILogger<UserRatingsRefreshHostedService> _logger;
	private readonly SemaphoreSlim _executionGate = new(1, 1);

	public UserRatingsRefreshHostedService(
		IServiceScopeFactory scopeFactory,
		ILogger<UserRatingsRefreshHostedService> logger)
	{
		_scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		var scheduleProperties = new (string Name, object? Value)[]
		{
			("JobName", JobName),
			("DefaultHour", DefaultScheduledHour),
			("DefaultMinute", DefaultScheduledMinute),
			("TimeZoneId", MoscowTimeZone.Id),
			("PollIntervalSeconds", (int)PollInterval.TotalSeconds),
		};

		using var scope = _logger.BeginExecutionScope(
			DomainLogEvents.UserRatings.RefreshSchedule,
			LogLayers.ApplicationBackground,
			ApplicationLogOperations.UserRatings.RefreshSchedule,
			scheduleProperties);

		_logger.LogCompleted(
			LogLevel.Information,
			DomainLogEvents.UserRatings.RefreshSchedule,
			LogLayers.ApplicationBackground,
			ApplicationLogOperations.UserRatings.RefreshSchedule,
			"User ratings refresh scheduler started.",
			scheduleProperties);

		await TryRunIfDueAsync(stoppingToken);

		using var timer = new PeriodicTimer(PollInterval);
		while(await timer.WaitForNextTickAsync(stoppingToken))
		{
			try
			{
				await TryRunIfDueAsync(stoppingToken);
			}
			catch(OperationCanceledException) when(stoppingToken.IsCancellationRequested)
			{
				throw;
			}
			catch(Exception ex)
			{
				_logger.LogFailed(
					LogLevel.Error,
					DomainLogEvents.UserRatings.RefreshRun,
					LogLayers.ApplicationBackground,
					ApplicationLogOperations.UserRatings.RefreshRun,
					"User ratings refresh iteration failed.",
					ex,
					scheduleProperties);
			}
		}
	}

	public async Task<UserRatingsRefreshScheduleModel> GetScheduleAsync(CancellationToken cancellationToken)
	{
		await using var serviceScope = _scopeFactory.CreateAsyncScope();
		var repository = serviceScope.ServiceProvider.GetRequiredService<IUserRatingRepository>();

		return await repository.GetScheduleAsync(
			JobName,
			DefaultScheduledHour,
			DefaultScheduledMinute,
			cancellationToken);
	}

	public async Task<UserRatingsRefreshScheduleModel> SetScheduleAsync(
		int hour,
		int minute,
		CancellationToken cancellationToken)
	{
		if(hour is < 0 or > 23)
		{
			throw new ArgumentOutOfRangeException(nameof(hour));
		}

		if(minute is < 0 or > 59)
		{
			throw new ArgumentOutOfRangeException(nameof(minute));
		}

		await using var serviceScope = _scopeFactory.CreateAsyncScope();
		var repository = serviceScope.ServiceProvider.GetRequiredService<IUserRatingRepository>();

		return await repository.SetScheduleAsync(JobName, hour, minute, cancellationToken);
	}

	public Task<UserRatingsRefreshRunModel> RunNowAsync(CancellationToken cancellationToken)
		=> ExecuteRefreshAsync(isManualRun: true, cancellationToken);

	private async Task TryRunIfDueAsync(CancellationToken cancellationToken)
	{
		await _executionGate.WaitAsync(cancellationToken);
		try
		{
			await using var serviceScope = _scopeFactory.CreateAsyncScope();
			var repository = serviceScope.ServiceProvider.GetRequiredService<IUserRatingRepository>();
			var schedule = await repository.GetScheduleAsync(
				JobName,
				DefaultScheduledHour,
				DefaultScheduledMinute,
				cancellationToken);

			var nowUtc = DateTimeOffset.UtcNow;
			var scheduledAtUtc = BuildScheduledRunUtc(nowUtc, schedule.Hour, schedule.Minute);

			if(nowUtc < scheduledAtUtc)
			{
				return;
			}

			if(schedule.LastCompletedAtUtc.HasValue
			   && schedule.LastCompletedAtUtc.Value >= scheduledAtUtc)
			{
				return;
			}

			await ExecuteRefreshCoreAsync(
				repository,
				schedule,
				nowUtc,
				scheduledAtUtc,
				isManualRun: false,
				cancellationToken);
		}
		finally
		{
			_executionGate.Release();
		}
	}

	private async Task<UserRatingsRefreshRunModel> ExecuteRefreshAsync(
		bool isManualRun,
		CancellationToken cancellationToken)
	{
		await _executionGate.WaitAsync(cancellationToken);
		try
		{
			await using var serviceScope = _scopeFactory.CreateAsyncScope();
			var repository = serviceScope.ServiceProvider.GetRequiredService<IUserRatingRepository>();
			var schedule = await repository.GetScheduleAsync(
				JobName,
				DefaultScheduledHour,
				DefaultScheduledMinute,
				cancellationToken);

			var nowUtc = DateTimeOffset.UtcNow;
			var scheduledAtUtc = BuildScheduledRunUtc(nowUtc, schedule.Hour, schedule.Minute);

			return await ExecuteRefreshCoreAsync(
				repository,
				schedule,
				nowUtc,
				scheduledAtUtc,
				isManualRun,
				cancellationToken);
		}
		finally
		{
			_executionGate.Release();
		}
	}

	private async Task<UserRatingsRefreshRunModel> ExecuteRefreshCoreAsync(
		IUserRatingRepository repository,
		UserRatingsRefreshScheduleModel schedule,
		DateTimeOffset nowUtc,
		DateTimeOffset scheduledAtUtc,
		bool isManualRun,
		CancellationToken cancellationToken)
	{
		var nowMoscow = TimeZoneInfo.ConvertTime(nowUtc, MoscowTimeZone);
		var scheduledAtMoscow = TimeZoneInfo.ConvertTime(scheduledAtUtc, MoscowTimeZone);
		var scheduledLocalDate = DateOnly.FromDateTime(scheduledAtMoscow.DateTime);
		var invocationKind = isManualRun ? "manual" : "scheduled";
		var startedAtUtc = DateTimeOffset.UtcNow;

		var properties = new (string Name, object? Value)[]
		{
			("JobName", JobName),
			("InvocationKind", invocationKind),
			("TimeZoneId", MoscowTimeZone.Id),
			("NowUtc", nowUtc),
			("NowMoscow", nowMoscow),
			("ScheduledLocalDate", scheduledLocalDate),
			("ScheduledHourMoscow", schedule.Hour),
			("ScheduledMinuteMoscow", schedule.Minute),
			("ScheduledAtUtc", scheduledAtUtc),
			("ScheduledAtMoscow", scheduledAtMoscow),
			("LastCompletedLocalDate", schedule.LastCompletedLocalDate),
		};

		using var scope = _logger.BeginExecutionScope(
			DomainLogEvents.UserRatings.RefreshRun,
			LogLayers.ApplicationBackground,
			ApplicationLogOperations.UserRatings.RefreshRun,
			properties);

		_logger.LogStarted(
			DomainLogEvents.UserRatings.RefreshRun,
			LogLayers.ApplicationBackground,
			ApplicationLogOperations.UserRatings.RefreshRun,
			"User ratings refresh started.",
			properties);

		var result = await repository.RecalculateRanksAsync(cancellationToken); var completedAtUtc = DateTimeOffset.UtcNow;
		await repository.SetLastCompletedLocalDateAsync(
			JobName,
			scheduledLocalDate,
			completedAtUtc,
			cancellationToken);

		_logger.LogCompleted(
			LogLevel.Information,
			DomainLogEvents.UserRatings.RefreshRun,
			LogLayers.ApplicationBackground,
			ApplicationLogOperations.UserRatings.RefreshRun,
			"User ratings refresh completed.",
			StructuredLog.Combine(
				properties,
				("StartedAtUtc", startedAtUtc),
				("TotalUsers", result.TotalUsers),
				("CreatedMissingRows", result.CreatedMissingRows),
				("UpdatedUsers", result.UpdatedUsers),
				("CompletedAtUtc", completedAtUtc)));

		return new UserRatingsRefreshRunModel(
			startedAtUtc,
			completedAtUtc,
			result.TotalUsers,
			result.CreatedMissingRows,
			result.UpdatedUsers);
	}

	private static DateTimeOffset BuildScheduledRunUtc(
		DateTimeOffset nowUtc,
		int scheduledHour,
		int scheduledMinute)
	{
		var nowMoscow = TimeZoneInfo.ConvertTime(nowUtc, MoscowTimeZone);
		var scheduledLocalDateTime = new DateTime(
			nowMoscow.Year,
			nowMoscow.Month,
			nowMoscow.Day,
			scheduledHour,
			scheduledMinute,
			0,
			DateTimeKind.Unspecified);

		var scheduledUtc = TimeZoneInfo.ConvertTimeToUtc(
			scheduledLocalDateTime,
			MoscowTimeZone);

		return new DateTimeOffset(scheduledUtc, TimeSpan.Zero);
	}

	private static TimeZoneInfo ResolveMoscowTimeZone()
	{
		try
		{
			return TimeZoneInfo.FindSystemTimeZoneById("Europe/Moscow");
		}
		catch(TimeZoneNotFoundException)
		{
			return TimeZoneInfo.FindSystemTimeZoneById("Russian Standard Time");
		}
		catch(InvalidTimeZoneException)
		{
			return TimeZoneInfo.FindSystemTimeZoneById("Russian Standard Time");
		}
	}
}