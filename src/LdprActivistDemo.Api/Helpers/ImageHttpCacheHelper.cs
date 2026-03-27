using LdprActivistDemo.Application.Images.Models;

using Microsoft.Extensions.Primitives;

namespace LdprActivistDemo.Api.Helpers;

/// <summary>
/// Централизованно настраивает HTTP-кэширование и revalidation для ответов с изображениями.
/// </summary>
public static class ImageHttpCacheHelper
{
	private const string CacheControlHeader = "Cache-Control";
	private const string ETagHeader = "ETag";
	private const string CacheControlValue = "no-cache, max-age=0, must-revalidate";

	/// <summary>
	/// Применяет ETag и cache-control заголовки к ответу и определяет,
	/// можно ли вернуть <c>304 Not Modified</c>.
	/// </summary>
	/// <param name="request">Текущий HTTP-запрос.</param>
	/// <param name="response">Текущий HTTP-ответ.</param>
	/// <param name="image">Данные изображения, используемые для построения ETag.</param>
	/// <returns>
	/// <see langword="true"/>, если клиент прислал совпадающий <c>If-None-Match</c>
	/// и можно вернуть <c>304 Not Modified</c>; иначе <see langword="false"/>.
	/// </returns>
	public static bool IsNotModified(
		HttpRequest request,
		HttpResponse response,
		ImagePayload image)
	{
		ArgumentNullException.ThrowIfNull(request);
		ArgumentNullException.ThrowIfNull(response);
		ArgumentNullException.ThrowIfNull(image);

		var etag = BuildEtag(image.Id);

		response.Headers[CacheControlHeader] = CacheControlValue;
		response.Headers[ETagHeader] = etag;

		return request.Headers.TryGetValue("If-None-Match", out var ifNoneMatch)
			&& ContainsEtag(ifNoneMatch, etag);
	}

	private static string BuildEtag(Guid imageId)
		=> $"\"{imageId:N}\"";

	private static bool ContainsEtag(StringValues headerValues, string expectedEtag)
	{
		for(var i = 0; i < headerValues.Count; i++)
		{
			var rawHeaderValue = headerValues[i];
			if(string.IsNullOrWhiteSpace(rawHeaderValue))
			{
				continue;
			}

			var candidates = rawHeaderValue.Split(
				',',
				StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

			for(var j = 0; j < candidates.Length; j++)
			{
				var current = candidates[j];

				if(string.Equals(current, expectedEtag, StringComparison.Ordinal)
				   || string.Equals(current, "*", StringComparison.Ordinal))
				{
					return true;
				}
			}
		}

		return false;
	}
}