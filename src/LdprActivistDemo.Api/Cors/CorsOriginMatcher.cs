namespace LdprActivistDemo.Api.Cors;

/// <summary>
/// Централизованно определяет, разрешен ли origin для CORS-политики API.
/// </summary>
public static class CorsOriginMatcher
{
	private static readonly HashSet<string> LoopbackHosts = new(StringComparer.OrdinalIgnoreCase)
	{
		"localhost",
		"127.0.0.1",
		"::1",
	};

	/// <summary>
	/// Проверяет, разрешен ли переданный origin.
	/// </summary>
	/// <param name="origin">Origin из HTTP-заголовка <c>Origin</c>.</param>
	/// <param name="configuredAllowedOrigins">Явно разрешенные origins из конфигурации.</param>
	/// <param name="allowDevelopmentLoopbackOrigins">
	/// Разрешать ли loopback-origins для локальной разработки.
	/// </param>
	/// <returns>
	/// <see langword="true"/>, если origin допустим для текущей политики; иначе <see langword="false"/>.
	/// </returns>
	public static bool IsAllowed(
		string? origin,
		IReadOnlyCollection<string> configuredAllowedOrigins,
		bool allowDevelopmentLoopbackOrigins)
	{
		if(string.IsNullOrWhiteSpace(origin))
		{
			return false;
		}

		var normalizedOrigin = NormalizeOrigin(origin);
		if(configuredAllowedOrigins.Any(candidate =>
			   string.Equals(
				   NormalizeOrigin(candidate),
				   normalizedOrigin,
				   StringComparison.OrdinalIgnoreCase)))
		{
			return true;
		}

		if(!allowDevelopmentLoopbackOrigins)
		{
			return false;
		}

		if(!Uri.TryCreate(normalizedOrigin, UriKind.Absolute, out var uri))
		{
			return false;
		}

		var isSupportedScheme =
			string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
			|| string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);

		return isSupportedScheme && LoopbackHosts.Contains(uri.Host);
	}

	private static string NormalizeOrigin(string origin)
		=> (origin ?? string.Empty)
			.Trim()
			.TrimEnd('/');
}