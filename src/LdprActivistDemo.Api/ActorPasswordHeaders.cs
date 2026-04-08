using System.Text;

namespace LdprActivistDemo.Api;

public static class ActorPasswordHeaders
{
	public const string RawHeaderName = "X-Actor-Password";
	public const string Base64HeaderName = "X-Actor-Password-Base64";
	public const string SupportedHeadersDisplay = $"{RawHeaderName} or {Base64HeaderName}";

	public static bool TryDecodeBase64(string? encodedValue, out string? password)
	{
		password = null;

		var normalizedValue = (encodedValue ?? string.Empty).Trim();
		if(normalizedValue.Length == 0)
		{
			return false;
		}

		try
		{
			var bytes = Convert.FromBase64String(normalizedValue);
			password = Encoding.UTF8.GetString(bytes);
			return true;
		}
		catch(FormatException)
		{
			return false;
		}
	}
}