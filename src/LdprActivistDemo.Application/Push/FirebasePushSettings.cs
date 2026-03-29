using Microsoft.Extensions.Configuration;

namespace LdprActivistDemo.Application.Push;

internal sealed class FirebasePushSettings
{
	public const string SectionName = "FirebasePush";
	private const string DefaultBaseUrl = "https://fcm.googleapis.com";

	public bool Enabled { get; init; } = true;
	public string BaseUrl { get; init; } = DefaultBaseUrl;
	public string? ProjectId { get; init; }
	public string? ServiceAccountJson { get; init; }
	public string? ServiceAccountJsonPath { get; init; }

	public static FirebasePushSettings Load(IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(configuration);

		var section = configuration.GetSection(SectionName);

		return new FirebasePushSettings
		{
			Enabled = ParseBool(section["Enabled"], fallback: true),
			BaseUrl = NormalizeOptional(section["BaseUrl"]) ?? DefaultBaseUrl,
			ProjectId = NormalizeOptional(section["ProjectId"]),
			ServiceAccountJson = NormalizeOptional(section["ServiceAccountJson"]),
			ServiceAccountJsonPath = NormalizeOptional(section["ServiceAccountJsonPath"]),
		};
	}

	private static bool ParseBool(string? raw, bool fallback)
	{
		if(string.IsNullOrWhiteSpace(raw))
		{
			return fallback;
		}

		return bool.TryParse(raw.Trim(), out var value)
			? value
			: fallback;
	}

	private static string? NormalizeOptional(string? value)
	{
		var normalized = (value ?? string.Empty).Trim();
		return normalized.Length == 0 ? null : normalized;
	}
}