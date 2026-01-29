using System.Text.Json;

using LdprActivistDemo.Application.PasswordReset;

using Microsoft.Extensions.Caching.Distributed;

namespace LdprActivistDemo.Persistence;

public sealed class RedisPasswordResetStore : IPasswordResetStore
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	private readonly IDistributedCache _cache;

	public RedisPasswordResetStore(IDistributedCache cache)
	{
		_cache = cache ?? throw new ArgumentNullException(nameof(cache));
	}

	public Task SetAsync(string phoneNumber, PasswordResetEntry entry, TimeSpan ttl, CancellationToken cancellationToken)
	{
		var key = BuildKey(phoneNumber);
		var json = JsonSerializer.Serialize(entry, JsonOptions);

		var options = new DistributedCacheEntryOptions
		{
			AbsoluteExpirationRelativeToNow = ttl,
		};

		return _cache.SetStringAsync(key, json, options, cancellationToken);
	}

	public async Task<PasswordResetEntry?> GetAsync(string phoneNumber, CancellationToken cancellationToken)
	{
		var key = BuildKey(phoneNumber);
		var json = await _cache.GetStringAsync(key, cancellationToken);

		if(string.IsNullOrWhiteSpace(json))
		{
			return null;
		}

		try
		{
			return JsonSerializer.Deserialize<PasswordResetEntry>(json, JsonOptions);
		}
		catch
		{
			return null;
		}
	}

	public Task RemoveAsync(string phoneNumber, CancellationToken cancellationToken)
	{
		var key = BuildKey(phoneNumber);
		return _cache.RemoveAsync(key, cancellationToken);
	}

	private static string BuildKey(string phoneNumber)
	{
		phoneNumber = (phoneNumber ?? string.Empty).Trim();
		return $"pwdreset:{phoneNumber}";
	}
}