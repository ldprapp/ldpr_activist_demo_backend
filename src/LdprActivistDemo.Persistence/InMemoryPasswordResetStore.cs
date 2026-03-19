using System.Collections.Concurrent;
using System.Text.Json;

using LdprActivistDemo.Application.Diagnostics;
using LdprActivistDemo.Application.Logging;
using LdprActivistDemo.Application.PasswordReset;
using LdprActivistDemo.Persistence.Logging;

using Microsoft.Extensions.Logging;

namespace LdprActivistDemo.Persistence;

public sealed class InMemoryPasswordResetStore : IPasswordResetStore
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.Ordinal);
	private readonly ILogger<InMemoryPasswordResetStore> _logger;

	public InMemoryPasswordResetStore(ILogger<InMemoryPasswordResetStore> logger)
	{
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	public Task SetAsync(string phoneNumber, PasswordResetEntry entry, TimeSpan ttl, CancellationToken cancellationToken)
	{
		var normalizedPhoneNumber = (phoneNumber ?? string.Empty).Trim();
		var properties = new (string Name, object? Value)[]
		{
			("PhoneNumber", MaskPhoneNumber(normalizedPhoneNumber)),
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
			"Password reset store set started.",
			properties);

		try
		{
			cancellationToken.ThrowIfCancellationRequested();

			var key = BuildKey(normalizedPhoneNumber);
			var expiresAt = DateTimeOffset.UtcNow.Add(ttl);
			var json = JsonSerializer.Serialize(entry, JsonOptions);

			_entries[key] = new Entry(json, expiresAt);

			_logger.LogCompleted(
				LogLevel.Debug,
				DomainLogEvents.PasswordReset.Store.Set,
				LogLayers.PersistenceRepository,
				PersistenceLogOperations.PasswordReset.Store.Set,
				"Password reset store set completed.",
				StructuredLog.Combine(properties, ("ExpiresAt", expiresAt), ("UserId", entry.UserId)));

			return Task.CompletedTask;
		}
		catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
		{
			_logger.LogAborted(
				LogLevel.Information,
				DomainLogEvents.PasswordReset.Store.Set,
				LogLayers.PersistenceRepository,
				PersistenceLogOperations.PasswordReset.Store.Set,
				"Password reset store set aborted.",
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
				"Password reset store set failed.",
				ex,
				properties);
			throw;
		}
	}

	public Task<PasswordResetEntry?> GetAsync(string phoneNumber, CancellationToken cancellationToken)
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
			"Password reset store get started.",
			properties);

		try
		{
			cancellationToken.ThrowIfCancellationRequested();

			var key = BuildKey(normalizedPhoneNumber);
			if(!_entries.TryGetValue(key, out var entry))
			{
				_logger.LogCompleted(
					LogLevel.Debug,
					DomainLogEvents.PasswordReset.Store.Get,
					LogLayers.PersistenceRepository,
					PersistenceLogOperations.PasswordReset.Store.Get,
					"Password reset store get completed.",
					StructuredLog.Combine(properties, ("Found", false), ("Expired", false)));

				return Task.FromResult<PasswordResetEntry?>(null);
			}

			if(entry.ExpiresAt <= DateTimeOffset.UtcNow)
			{
				_entries.TryRemove(key, out _);

				_logger.LogCompleted(
					LogLevel.Debug,
					DomainLogEvents.PasswordReset.Store.Get,
					LogLayers.PersistenceRepository,
					PersistenceLogOperations.PasswordReset.Store.Get,
					"Password reset store get completed.",
					StructuredLog.Combine(properties, ("Found", false), ("Expired", true)));

				return Task.FromResult<PasswordResetEntry?>(null);
			}

			var parsed = JsonSerializer.Deserialize<PasswordResetEntry>(entry.Json, JsonOptions);
			if(parsed is null)
			{
				_entries.TryRemove(key, out _);

				_logger.LogRejected(
					LogLevel.Warning,
					DomainLogEvents.PasswordReset.Store.Get,
					LogLayers.PersistenceRepository,
					PersistenceLogOperations.PasswordReset.Store.Get,
					"Password reset store get rejected. Entry deserialization failed.",
					StructuredLog.Combine(properties, ("RejectedReason", "DeserializationFailed")));

				return Task.FromResult<PasswordResetEntry?>(null);
			}

			_logger.LogCompleted(
				LogLevel.Debug,
				DomainLogEvents.PasswordReset.Store.Get,
				LogLayers.PersistenceRepository,
				PersistenceLogOperations.PasswordReset.Store.Get,
				"Password reset store get completed.",
				StructuredLog.Combine(properties, ("Found", true), ("UserId", parsed.UserId)));

			return Task.FromResult<PasswordResetEntry?>(parsed);
		}
		catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
		{
			_logger.LogAborted(
				LogLevel.Information,
				DomainLogEvents.PasswordReset.Store.Get,
				LogLayers.PersistenceRepository,
				PersistenceLogOperations.PasswordReset.Store.Get,
				"Password reset store get aborted.",
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
				"Password reset store get failed.",
				ex,
				properties);
			throw;
		}
	}

	public Task RemoveAsync(string phoneNumber, CancellationToken cancellationToken)
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
			"Password reset store remove started.",
			properties);

		try
		{
			cancellationToken.ThrowIfCancellationRequested();

			var key = BuildKey(normalizedPhoneNumber);
			var removed = _entries.TryRemove(key, out _);

			_logger.LogCompleted(
				LogLevel.Debug,
				DomainLogEvents.PasswordReset.Store.Remove,
				LogLayers.PersistenceRepository,
				PersistenceLogOperations.PasswordReset.Store.Remove,
				"Password reset store remove completed.",
				StructuredLog.Combine(properties, ("Removed", removed)));

			return Task.CompletedTask;
		}
		catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
		{
			_logger.LogAborted(
				LogLevel.Information,
				DomainLogEvents.PasswordReset.Store.Remove,
				LogLayers.PersistenceRepository,
				PersistenceLogOperations.PasswordReset.Store.Remove,
				"Password reset store remove aborted.",
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
				"Password reset store remove failed.",
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

	private sealed record Entry(string Json, DateTimeOffset ExpiresAt);
}