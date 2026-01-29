using System.Collections.Concurrent;
using System.Text.Json;

using LdprActivistDemo.Application.PasswordReset;

namespace LdprActivistDemo.Persistence;

public sealed class InMemoryPasswordResetStore : IPasswordResetStore
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.Ordinal);

	public Task SetAsync(string phoneNumber, PasswordResetEntry entry, TimeSpan ttl, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var key = BuildKey(phoneNumber);
		var expiresAt = DateTimeOffset.UtcNow.Add(ttl);
		var json = JsonSerializer.Serialize(entry, JsonOptions);

		_entries[key] = new Entry(json, expiresAt);
		return Task.CompletedTask;
	}

	public Task<PasswordResetEntry?> GetAsync(string phoneNumber, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var key = BuildKey(phoneNumber);
		if(!_entries.TryGetValue(key, out var entry))
		{
			return Task.FromResult<PasswordResetEntry?>(null);
		}

		if(entry.ExpiresAt <= DateTimeOffset.UtcNow)
		{
			_entries.TryRemove(key, out _);
			return Task.FromResult<PasswordResetEntry?>(null);
		}

		try
		{
			var parsed = JsonSerializer.Deserialize<PasswordResetEntry>(entry.Json, JsonOptions);
			return Task.FromResult<PasswordResetEntry?>(parsed);
		}
		catch
		{
			return Task.FromResult<PasswordResetEntry?>(null);
		}
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
		return $"pwdreset:{phoneNumber}";
	}

	private sealed record Entry(string Json, DateTimeOffset ExpiresAt);
}