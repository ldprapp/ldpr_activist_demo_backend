using System.Collections.Concurrent;

using LdprActivistDemo.Application.Otp;

namespace LdprActivistDemo.Persistence;

public sealed class InMemoryOtpStore : IOtpStore
{
	private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.Ordinal);

	public Task SetAsync(string phoneNumber, string code, TimeSpan ttl, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var key = BuildKey(phoneNumber);
		var expiresAt = DateTimeOffset.UtcNow.Add(ttl);

		_entries[key] = new Entry(code, expiresAt);
		return Task.CompletedTask;
	}

	public Task<string?> GetAsync(string phoneNumber, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var key = BuildKey(phoneNumber);
		if(!_entries.TryGetValue(key, out var entry))
		{
			return Task.FromResult<string?>(null);
		}

		if(entry.ExpiresAt <= DateTimeOffset.UtcNow)
		{
			_entries.TryRemove(key, out _);
			return Task.FromResult<string?>(null);
		}

		return Task.FromResult<string?>(entry.Code);
	}

	public Task RemoveAsync(string phoneNumber, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var key = BuildKey(phoneNumber);
		_entries.TryRemove(key, out _);
		return Task.CompletedTask;
	}

	private static string BuildKey(string phoneNumber)
	{
		phoneNumber = (phoneNumber ?? string.Empty).Trim();
		return $"otp:{phoneNumber}";
	}

	private sealed record Entry(string Code, DateTimeOffset ExpiresAt);
}