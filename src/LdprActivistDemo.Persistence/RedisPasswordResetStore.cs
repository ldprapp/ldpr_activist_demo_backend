using System.Text.Json;

using LdprActivistDemo.Application.Diagnostics;
using LdprActivistDemo.Application.Logging;
using LdprActivistDemo.Application.PasswordReset;
using LdprActivistDemo.Persistence.Logging;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace LdprActivistDemo.Persistence;

public sealed class RedisPasswordResetStore : IPasswordResetStore
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	private readonly IDistributedCache _cache;
	private readonly ILogger<RedisPasswordResetStore> _logger;

	public RedisPasswordResetStore(
		IDistributedCache cache,
		ILogger<RedisPasswordResetStore> logger)
	{
		_cache = cache ?? throw new ArgumentNullException(nameof(cache));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	public async Task SetAsync(string phoneNumber, PasswordResetEntry entry, TimeSpan ttl, CancellationToken cancellationToken)
	{
		var normalizedPhoneNumber = (phoneNumber ?? string.Empty).Trim();
		var properties = new (string Name, object? Value)[]
		{
			("PhoneNumber", MaskPhoneNumber(normalizedPhoneNumber)),
			("UserId", entry.UserId),
			("TtlSeconds", (int)Math.Max(0, ttl.TotalSeconds)),
		};

		using var scope = _logger.BeginExecutionScope(
			DomainLogEvents.PasswordReset.Store.Set,
			LogLayers.PersistenceRepository,
			PersistenceLogOperations.PasswordReset.Store.Set,
			properties);

		_logger.LogStarted(
			DomainLogEvents.PasswordReset.Store.Set,
			LogLayers.PersistenceRepository,
			PersistenceLogOperations.PasswordReset.Store.Set,
			"Password reset redis store set started.",
			properties);

		try
		{
			var key = BuildKey(normalizedPhoneNumber);
			var json = JsonSerializer.Serialize(entry, JsonOptions);

			var options = new DistributedCacheEntryOptions
			{
				AbsoluteExpirationRelativeToNow = ttl,
			};

			await _cache.SetStringAsync(key, json, options, cancellationToken);

			_logger.LogCompleted(
				LogLevel.Debug,
				DomainLogEvents.PasswordReset.Store.Set,
				LogLayers.PersistenceRepository,
				PersistenceLogOperations.PasswordReset.Store.Set,
				"Password reset redis store set completed.",
				properties);
		}
		catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
		{
			_logger.LogAborted(
				LogLevel.Information,
				DomainLogEvents.PasswordReset.Store.Set,
				LogLayers.PersistenceRepository,
				PersistenceLogOperations.PasswordReset.Store.Set,
				"Password reset redis store set aborted.",
				properties);
			throw;
		}
		catch(Exception ex)
		{
			_logger.LogFailed(
				LogLevel.Error,
				DomainLogEvents.PasswordReset.Store.Set,
				LogLayers.PersistenceRepository,
				PersistenceLogOperations.PasswordReset.Store.Set,
				"Password reset redis store set failed.",
				ex,
				properties);
			throw;
		}
	}

	public async Task<PasswordResetEntry?> GetAsync(string phoneNumber, CancellationToken cancellationToken)
	{
		var normalizedPhoneNumber = (phoneNumber ?? string.Empty).Trim();
		var properties = new (string Name, object? Value)[]
		{
			("PhoneNumber", MaskPhoneNumber(normalizedPhoneNumber)),
		};

		using var scope = _logger.BeginExecutionScope(
			DomainLogEvents.PasswordReset.Store.Get,
			LogLayers.PersistenceRepository,
			PersistenceLogOperations.PasswordReset.Store.Get,
			properties);

		_logger.LogStarted(
			DomainLogEvents.PasswordReset.Store.Get,
			LogLayers.PersistenceRepository,
			PersistenceLogOperations.PasswordReset.Store.Get,
			"Password reset redis store get started.",
			properties);

		try
		{
			var key = BuildKey(normalizedPhoneNumber);
			var json = await _cache.GetStringAsync(key, cancellationToken);

			if(string.IsNullOrWhiteSpace(json))
			{
				_logger.LogCompleted(
					LogLevel.Debug,
					DomainLogEvents.PasswordReset.Store.Get,
					LogLayers.PersistenceRepository,
					PersistenceLogOperations.PasswordReset.Store.Get,
					"Password reset redis store get completed.",
					StructuredLog.Combine(properties, ("Found", false)));
				return null;
			}

			var parsed = JsonSerializer.Deserialize<PasswordResetEntry>(json, JsonOptions);
			if(parsed is null)
			{
				await _cache.RemoveAsync(key, cancellationToken);

				_logger.LogRejected(
					LogLevel.Warning,
					DomainLogEvents.PasswordReset.Store.Get,
					LogLayers.PersistenceRepository,
					PersistenceLogOperations.PasswordReset.Store.Get,
					"Password reset redis store get rejected. Entry deserialization failed.",
					StructuredLog.Combine(properties, ("RejectedReason", "DeserializationFailed")));

				return null;
			}

			_logger.LogCompleted(
				LogLevel.Debug,
				DomainLogEvents.PasswordReset.Store.Get,
				LogLayers.PersistenceRepository,
				PersistenceLogOperations.PasswordReset.Store.Get,
				"Password reset redis store get completed.",
				StructuredLog.Combine(properties, ("Found", true), ("UserId", parsed.UserId)));

			return parsed;
		}
		catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
		{
			_logger.LogAborted(
				LogLevel.Information,
				DomainLogEvents.PasswordReset.Store.Get,
				LogLayers.PersistenceRepository,
				PersistenceLogOperations.PasswordReset.Store.Get,
				"Password reset redis store get aborted.",
				properties);
			throw;
		}
		catch(Exception ex)
		{
			_logger.LogFailed(
				LogLevel.Error,
				DomainLogEvents.PasswordReset.Store.Get,
				LogLayers.PersistenceRepository,
				PersistenceLogOperations.PasswordReset.Store.Get,
				"Password reset redis store get failed.",
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
			DomainLogEvents.PasswordReset.Store.Remove,
			LogLayers.PersistenceRepository,
			PersistenceLogOperations.PasswordReset.Store.Remove,
			properties);

		_logger.LogStarted(
			DomainLogEvents.PasswordReset.Store.Remove,
			LogLayers.PersistenceRepository,
			PersistenceLogOperations.PasswordReset.Store.Remove,
			"Password reset redis store remove started.",
			properties);

		try
		{
			var key = BuildKey(normalizedPhoneNumber);
			await _cache.RemoveAsync(key, cancellationToken);

			_logger.LogCompleted(
				LogLevel.Debug,
				DomainLogEvents.PasswordReset.Store.Remove,
				LogLayers.PersistenceRepository,
				PersistenceLogOperations.PasswordReset.Store.Remove,
				"Password reset redis store remove completed.",
				properties);
		}
		catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
		{
			_logger.LogAborted(
				LogLevel.Information,
				DomainLogEvents.PasswordReset.Store.Remove,
				LogLayers.PersistenceRepository,
				PersistenceLogOperations.PasswordReset.Store.Remove,
				"Password reset redis store remove aborted.",
				properties);
			throw;
		}
		catch(Exception ex)
		{
			_logger.LogFailed(
				LogLevel.Error,
				DomainLogEvents.PasswordReset.Store.Remove,
				LogLayers.PersistenceRepository,
				PersistenceLogOperations.PasswordReset.Store.Remove,
				"Password reset redis store remove failed.",
				ex,
				properties);
			throw;
		}
	}

	private static string BuildKey(string phoneNumber)
	{
		phoneNumber = (phoneNumber ?? string.Empty).Trim();
		return $"pwdreset:{phoneNumber}";
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