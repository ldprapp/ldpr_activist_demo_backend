using LdprActivistDemo.Application.Diagnostics;
using LdprActivistDemo.Application.Logging;
using LdprActivistDemo.Application.Otp;
using LdprActivistDemo.Persistence.Logging;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace LdprActivistDemo.Persistence;

public sealed class RedisOtpStore : IOtpStore
{
	private readonly IDistributedCache _cache;
	private readonly ILogger<RedisOtpStore> _logger;

	public RedisOtpStore(IDistributedCache cache, ILogger<RedisOtpStore> logger)
	{
		_cache = cache ?? throw new ArgumentNullException(nameof(cache));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	public async Task SetAsync(string phoneNumber, string code, TimeSpan ttl, CancellationToken cancellationToken)
	{
		var normalizedPhoneNumber = (phoneNumber ?? string.Empty).Trim();
		var properties = new (string Name, object? Value)[]
		{
			("PhoneNumber", MaskPhoneNumber(normalizedPhoneNumber)),
			("TtlSeconds", (int)Math.Max(0, ttl.TotalSeconds)),
		};

		using var scope = _logger.BeginExecutionScope(
			DomainLogEvents.Otp.Store.Set,
			LogLayers.PersistenceRepository,
			PersistenceLogOperations.OtpStore.Set,
			properties);

		_logger.LogStarted(
			DomainLogEvents.Otp.Store.Set,
			LogLayers.PersistenceRepository,
			PersistenceLogOperations.OtpStore.Set,
			"OTP redis store set started.",
			properties);

		try
		{
			var key = BuildKey(normalizedPhoneNumber);
			var options = new DistributedCacheEntryOptions
			{
				AbsoluteExpirationRelativeToNow = ttl,
			};

			await _cache.SetStringAsync(key, code, options, cancellationToken);

			_logger.LogCompleted(
				LogLevel.Debug,
				DomainLogEvents.Otp.Store.Set,
				LogLayers.PersistenceRepository,
				PersistenceLogOperations.OtpStore.Set,
				"OTP redis store set completed.",
				properties);
		}
		catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
		{
			_logger.LogAborted(
				LogLevel.Information,
				DomainLogEvents.Otp.Store.Set,
				LogLayers.PersistenceRepository,
				PersistenceLogOperations.OtpStore.Set,
				"OTP redis store set aborted.",
				properties);
			throw;
		}
		catch(Exception ex)
		{
			_logger.LogFailed(
				LogLevel.Error,
				DomainLogEvents.Otp.Store.Set,
				LogLayers.PersistenceRepository,
				PersistenceLogOperations.OtpStore.Set,
				"OTP redis store set failed.",
				ex,
				properties);
			throw;
		}
	}

	public async Task<string?> GetAsync(string phoneNumber, CancellationToken cancellationToken)
	{
		var normalizedPhoneNumber = (phoneNumber ?? string.Empty).Trim();
		var properties = new (string Name, object? Value)[]
		{
			("PhoneNumber", MaskPhoneNumber(normalizedPhoneNumber)),
		};

		using var scope = _logger.BeginExecutionScope(
			DomainLogEvents.Otp.Store.Get,
			LogLayers.PersistenceRepository,
			PersistenceLogOperations.OtpStore.Get,
			properties);

		_logger.LogStarted(
			DomainLogEvents.Otp.Store.Get,
			LogLayers.PersistenceRepository,
			PersistenceLogOperations.OtpStore.Get,
			"OTP redis store get started.",
			properties);

		try
		{
			var key = BuildKey(normalizedPhoneNumber);
			var value = await _cache.GetStringAsync(key, cancellationToken);

			_logger.LogCompleted(
				LogLevel.Debug,
				DomainLogEvents.Otp.Store.Get,
				LogLayers.PersistenceRepository,
				PersistenceLogOperations.OtpStore.Get,
				"OTP redis store get completed.",
				StructuredLog.Combine(properties, ("Found", !string.IsNullOrWhiteSpace(value))));

			return value;
		}
		catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
		{
			_logger.LogAborted(
				LogLevel.Information,
				DomainLogEvents.Otp.Store.Get,
				LogLayers.PersistenceRepository,
				PersistenceLogOperations.OtpStore.Get,
				"OTP redis store get aborted.",
				properties);
			throw;
		}
		catch(Exception ex)
		{
			_logger.LogFailed(
				LogLevel.Error,
				DomainLogEvents.Otp.Store.Get,
				LogLayers.PersistenceRepository,
				PersistenceLogOperations.OtpStore.Get,
				"OTP redis store get failed.",
				ex,
				properties);
			throw;
		}
	}

	public async Task RemoveAsync(string phoneNumber, CancellationToken cancellationToken)
	{
		var normalizedPhoneNumber = (phoneNumber ?? string.Empty).Trim();
		var properties = new (string Name, object? Value)[]
		{
			("PhoneNumber", MaskPhoneNumber(normalizedPhoneNumber)),
		};

		using var scope = _logger.BeginExecutionScope(
			DomainLogEvents.Otp.Store.Remove,
			LogLayers.PersistenceRepository,
			PersistenceLogOperations.OtpStore.Remove,
			properties);

		_logger.LogStarted(
			DomainLogEvents.Otp.Store.Remove,
			LogLayers.PersistenceRepository,
			PersistenceLogOperations.OtpStore.Remove,
			"OTP redis store remove started.",
			properties);

		try
		{
			var key = BuildKey(normalizedPhoneNumber);
			await _cache.RemoveAsync(key, cancellationToken);

			_logger.LogCompleted(
				LogLevel.Debug,
				DomainLogEvents.Otp.Store.Remove,
				LogLayers.PersistenceRepository,
				PersistenceLogOperations.OtpStore.Remove,
				"OTP redis store remove completed.",
				properties);
		}
		catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
		{
			_logger.LogAborted(
				LogLevel.Information,
				DomainLogEvents.Otp.Store.Remove,
				LogLayers.PersistenceRepository,
				PersistenceLogOperations.OtpStore.Remove,
				"OTP redis store remove aborted.",
				properties);
			throw;
		}
		catch(Exception ex)
		{
			_logger.LogFailed(
				LogLevel.Error,
				DomainLogEvents.Otp.Store.Remove,
				LogLayers.PersistenceRepository,
				PersistenceLogOperations.OtpStore.Remove,
				"OTP redis store remove failed.",
				ex,
				properties);
			throw;
		}
	}

	private static string BuildKey(string phoneNumber)
	{
		phoneNumber = (phoneNumber ?? string.Empty).Trim();
		return $"otp:{phoneNumber}";
	}

	private static string MaskPhoneNumber(string? value)
	{
		var normalized = (value ?? string.Empty).Trim();
		if(normalized.Length <= 4)
		{
			return "****";
		}

		return $"***{normalized[^4..]}";
	}
}