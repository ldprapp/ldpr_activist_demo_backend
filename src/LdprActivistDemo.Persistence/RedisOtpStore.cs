using LdprActivistDemo.Application.Otp;

using Microsoft.Extensions.Caching.Distributed;

namespace LdprActivistDemo.Persistence;

public sealed class RedisOtpStore : IOtpStore
{
	private readonly IDistributedCache _cache;

	public RedisOtpStore(IDistributedCache cache)
	{
		_cache = cache;
	}

	public Task SetAsync(string phoneNumber, string code, TimeSpan ttl, CancellationToken cancellationToken)
	{
		var key = BuildKey(phoneNumber);
		var options = new DistributedCacheEntryOptions
		{
			AbsoluteExpirationRelativeToNow = ttl,
		};

		return _cache.SetStringAsync(key, code, options, cancellationToken);
	}

	public Task<string?> GetAsync(string phoneNumber, CancellationToken cancellationToken)
	{
		var key = BuildKey(phoneNumber);
		return _cache.GetStringAsync(key, cancellationToken);
	}

	public Task RemoveAsync(string phoneNumber, CancellationToken cancellationToken)
	{
		var key = BuildKey(phoneNumber);
		return _cache.RemoveAsync(key, cancellationToken);
	}

	private static string BuildKey(string phoneNumber)
	{
		phoneNumber = phoneNumber.Trim();
		return $"otp:{phoneNumber}";
	}
}