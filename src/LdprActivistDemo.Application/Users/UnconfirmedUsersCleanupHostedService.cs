using System.Collections.Concurrent;

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
		_dueAtByUserId[userId] = DateTimeOffset.UtcNow.Add(delay);
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
			_logger.LogWarning(
				"Unconfirmed user cleanup delay is {DelayMinutes} minutes (OTP TTL = {OtpTtlSeconds} seconds).",
				(int)delay.TotalMinutes,
				otpTtlSeconds);
		}

		return delay;
	}

	private async Task CleanupDueAsync(CancellationToken cancellationToken)
	{
		if(_dueAtByUserId.IsEmpty)
		{
			return;
		}

		var now = DateTimeOffset.UtcNow;
		List<Guid>? due = null;

		foreach(var kvp in _dueAtByUserId)
		{
			if(kvp.Value <= now)
			{
				due ??= new List<Guid>();
				due.Add(kvp.Key);
			}
		}

		if(due is null || due.Count == 0)
		{
			return;
		}

		foreach(var userId in due)
		{
			_dueAtByUserId.TryRemove(userId, out _);
		}

		await using var scope = _scopeFactory.CreateAsyncScope();
		var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();

		foreach(var userId in due)
		{
			try
			{
				var removed = await users.DeleteUnconfirmedByIdAsync(userId, cancellationToken);
				if(removed)
				{
					_logger.LogInformation("Unconfirmed user removed by timer. UserId={UserId}.", userId);
				}
			}
			catch(Exception ex)
			{
				_logger.LogError(ex, "Failed to cleanup user by timer. UserId={UserId}.", userId);
			}
		}
	}
}