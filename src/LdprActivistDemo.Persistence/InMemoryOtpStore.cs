using System.Collections.Concurrent;

using LdprActivistDemo.Application.Diagnostics;
using LdprActivistDemo.Application.Logging;
using LdprActivistDemo.Application.Otp;
using LdprActivistDemo.Persistence.Logging;

using Microsoft.Extensions.Logging;

namespace LdprActivistDemo.Persistence;

public sealed class InMemoryOtpStore : IOtpStore
{
	private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.Ordinal);
	private readonly ILogger<InMemoryOtpStore> _logger;

	public InMemoryOtpStore(ILogger<InMemoryOtpStore> logger)
	{
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	public Task SetAsync(string phoneNumber, string code, TimeSpan ttl, CancellationToken cancellationToken)
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
			"OTP store set started.",
			properties);

		try
		{
			cancellationToken.ThrowIfCancellationRequested();

			var key = BuildKey(normalizedPhoneNumber);
			var expiresAt = DateTimeOffset.UtcNow.Add(ttl);
			_entries[key] = new Entry(code, expiresAt);

			_logger.LogCompleted(
				LogLevel.Debug,
				DomainLogEvents.Otp.Store.Set,
				LogLayers.PersistenceRepository,
				PersistenceLogOperations.OtpStore.Set,
				"OTP store set completed.",
				StructuredLog.Combine(properties, ("ExpiresAt", expiresAt)));

			return Task.CompletedTask;
		}
		catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
		{
			_logger.LogAborted(
				LogLevel.Information,
				DomainLogEvents.Otp.Store.Set,
				LogLayers.PersistenceRepository,
				PersistenceLogOperations.OtpStore.Set,
				"OTP store set aborted.",
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
				"OTP store set failed.",
				ex,
				properties);
			throw;
		}
	}

	public Task<string?> GetAsync(string phoneNumber, CancellationToken cancellationToken)
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
			"OTP store get started.",
			properties);

		try
		{
			cancellationToken.ThrowIfCancellationRequested();

			var key = BuildKey(normalizedPhoneNumber);
			if(!_entries.TryGetValue(key, out var entry))
			{
				_logger.LogCompleted(
					LogLevel.Debug,
					DomainLogEvents.Otp.Store.Get,
					LogLayers.PersistenceRepository,
					PersistenceLogOperations.OtpStore.Get,
					"OTP store get completed.",
					StructuredLog.Combine(properties, ("Found", false), ("Expired", false)));

				return Task.FromResult<string?>(null);
			}

			if(entry.ExpiresAt <= DateTimeOffset.UtcNow)
			{
				_entries.TryRemove(key, out _);

				_logger.LogCompleted(
					LogLevel.Debug,
					DomainLogEvents.Otp.Store.Get,
					LogLayers.PersistenceRepository,
					PersistenceLogOperations.OtpStore.Get,
					"OTP store get completed.",
					StructuredLog.Combine(properties, ("Found", false), ("Expired", true)));

				return Task.FromResult<string?>(null);
			}

			_logger.LogCompleted(
				LogLevel.Debug,
				DomainLogEvents.Otp.Store.Get,
				LogLayers.PersistenceRepository,
				PersistenceLogOperations.OtpStore.Get,
				"OTP store get completed.",
				StructuredLog.Combine(properties, ("Found", true), ("Expired", false)));

			return Task.FromResult<string?>(entry.Code);
		}
		catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
		{
			_logger.LogAborted(
				LogLevel.Information,
				DomainLogEvents.Otp.Store.Get,
				LogLayers.PersistenceRepository,
				PersistenceLogOperations.OtpStore.Get,
				"OTP store get aborted.",
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
				"OTP store get failed.",
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
			DomainLogEvents.Otp.Store.Remove,
			LogLayers.PersistenceRepository,
			PersistenceLogOperations.OtpStore.Remove,
			properties);

		_logger.LogStarted(
			DomainLogEvents.Otp.Store.Remove,
			LogLayers.PersistenceRepository,
			PersistenceLogOperations.OtpStore.Remove,
			"OTP store remove started.",
			properties);

		try
		{
			cancellationToken.ThrowIfCancellationRequested();

			var key = BuildKey(normalizedPhoneNumber);
			var removed = _entries.TryRemove(key, out _);

			_logger.LogCompleted(
				LogLevel.Debug,
				DomainLogEvents.Otp.Store.Remove,
				LogLayers.PersistenceRepository,
				PersistenceLogOperations.OtpStore.Remove,
				"OTP store remove completed.",
				StructuredLog.Combine(properties, ("Removed", removed)));

			return Task.CompletedTask;
		}
		catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
		{
			_logger.LogAborted(
				LogLevel.Information,
				DomainLogEvents.Otp.Store.Remove,
				LogLayers.PersistenceRepository,
				PersistenceLogOperations.OtpStore.Remove,
				"OTP store remove aborted.",
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
				"OTP store remove failed.",
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

	private sealed record Entry(string Code, DateTimeOffset ExpiresAt);
}