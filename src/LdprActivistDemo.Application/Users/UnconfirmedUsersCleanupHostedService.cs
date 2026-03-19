using System.Collections.Concurrent;

using LdprActivistDemo.Application.Diagnostics;
using LdprActivistDemo.Application.Logging;
using LdprActivistDemo.Application.Otp;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LdprActivistDemo.Application.Users;

public sealed class UnconfirmedUsersCleanupHostedService : BackgroundService, IUnconfirmedUserCleanupScheduler
{
	private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);

	private readonly ConcurrentDictionary<Guid, DateTimeOffset> _dueAtByUserId = new();
	private readonly IServiceScopeFactory _scopeFactory;
	private readonly ILogger<UnconfirmedUsersCleanupHostedService> _logger;
	private readonly IOptions<OtpOptions> _otpOptions;

	public UnconfirmedUsersCleanupHostedService(
		IServiceScopeFactory scopeFactory,
		ILogger<UnconfirmedUsersCleanupHostedService> logger,
		IOptions<OtpOptions> otpOptions)
	{
		_scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_otpOptions = otpOptions ?? throw new ArgumentNullException(nameof(otpOptions));
	}

	public void Schedule(Guid userId)
	{
		if(userId == Guid.Empty)
		{
			return;
		}

		var delay = GetCleanupDelay();
		var dueAt = DateTimeOffset.UtcNow.Add(delay);
		_dueAtByUserId[userId] = dueAt;

		var properties = new (string Name, object? Value)[]
		{
			("UserId", userId),
			("DueAt", dueAt),
			("DelaySeconds", (int)delay.TotalSeconds),
			("ScheduledCount", _dueAtByUserId.Count),
		};

		using var scope = _logger.BeginExecutionScope(
			DomainLogEvents.User.CleanupSchedule,
			LogLayers.ApplicationBackground,
			ApplicationLogOperations.Users.CleanupSchedule,
			properties);

		_logger.LogCompleted(
			LogLevel.Debug,
			DomainLogEvents.User.CleanupSchedule,
			LogLayers.ApplicationBackground,
			ApplicationLogOperations.Users.CleanupSchedule,
			"Unconfirmed user cleanup scheduled.",
			properties);
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		using var timer = new PeriodicTimer(PollInterval);

		while(await timer.WaitForNextTickAsync(stoppingToken))
		{
			await CleanupDueAsync(stoppingToken);
		}
	}

	private TimeSpan GetCleanupDelay()
	{
		var baseline = TimeSpan.FromMinutes(10);

		var otpTtlSeconds = Math.Max(0, _otpOptions.Value.TtlSeconds);
		var otpTtl = TimeSpan.FromSeconds(otpTtlSeconds);
		var minRequired = otpTtl + TimeSpan.FromMinutes(10);

		var delay = baseline < minRequired ? minRequired : baseline;

		if(delay > TimeSpan.FromMinutes(30))
		{
			_logger.LogRejected(
				LogLevel.Warning,
				DomainLogEvents.User.CleanupSchedule,
				LogLayers.ApplicationBackground,
				ApplicationLogOperations.Users.CleanupSchedule,
				"Unconfirmed user cleanup delay is unexpectedly large.",
				("DelayMinutes", (int)delay.TotalMinutes),
				("OtpTtlSeconds", otpTtlSeconds));
		}

		return delay;
	}

	private async Task CleanupDueAsync(CancellationToken cancellationToken)
	{
		var properties = new (string Name, object? Value)[]
		{
			("ScheduledCount", _dueAtByUserId.Count),
		};

		using var scope = _logger.BeginExecutionScope(
			DomainLogEvents.User.CleanupRun,
			LogLayers.ApplicationBackground,
			ApplicationLogOperations.Users.CleanupRun,
			properties);

		_logger.LogStarted(
			DomainLogEvents.User.CleanupRun,
			LogLayers.ApplicationBackground,
			ApplicationLogOperations.Users.CleanupRun,
			"Unconfirmed users cleanup run started.",
			properties);

		if(_dueAtByUserId.IsEmpty)
		{
			_logger.LogCompleted(
				LogLevel.Debug,
				DomainLogEvents.User.CleanupRun,
				LogLayers.ApplicationBackground,
				ApplicationLogOperations.Users.CleanupRun,
				"Unconfirmed users cleanup run completed. Queue is empty.",
				properties);
			return;
		}

		var now = DateTimeOffset.UtcNow;
		List<Guid>? due = null;

		foreach(var kvp in _dueAtByUserId)
		{
			cancellationToken.ThrowIfCancellationRequested();

			if(kvp.Value <= now)
			{
				due ??= new List<Guid>();
				due.Add(kvp.Key);
			}
		}

		if(due is null || due.Count == 0)
		{
			_logger.LogCompleted(
				LogLevel.Debug,
				DomainLogEvents.User.CleanupRun,
				LogLayers.ApplicationBackground,
				ApplicationLogOperations.Users.CleanupRun,
				"Unconfirmed users cleanup run completed. No due users.",
				StructuredLog.Combine(properties, ("DueUsersCount", 0)));
			return;
		}

		var runProperties = StructuredLog.Combine(
			properties,
			("DueUsersCount", due.Count));

		foreach(var userId in due)
		{
			cancellationToken.ThrowIfCancellationRequested();

			_dueAtByUserId.TryRemove(userId, out _);
		}

		await using var serviceScope = _scopeFactory.CreateAsyncScope();
		var users = serviceScope.ServiceProvider.GetRequiredService<IUserRepository>();

		var removedCount = 0;
		var failedCount = 0;

		foreach(var userId in due)
		{
			cancellationToken.ThrowIfCancellationRequested();

			try
			{
				var removed = await users.DeleteUnconfirmedByIdAsync(userId, cancellationToken);
				if(removed)
				{
					removedCount++;
				}
			}
			catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
			{
				throw;
			}
			catch(Exception ex)
			{
				failedCount++;
				_logger.LogFailed(
					LogLevel.Error,
					DomainLogEvents.User.CleanupRun,
					LogLayers.ApplicationBackground,
					ApplicationLogOperations.Users.CleanupRun,
					"Failed to cleanup unconfirmed user by timer.",
					ex,
					StructuredLog.Combine(runProperties, ("UserId", userId)));
			}
		}

		_logger.LogCompleted(
			LogLevel.Information,
			DomainLogEvents.User.CleanupRun,
			LogLayers.ApplicationBackground,
			ApplicationLogOperations.Users.CleanupRun,
			"Unconfirmed users cleanup run completed.",
			StructuredLog.Combine(
				runProperties,
				("RemovedCount", removedCount),
				("FailedCount", failedCount)));
	}
}