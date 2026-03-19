using System.Collections;

using Microsoft.Extensions.Logging;

namespace LdprActivistDemo.Application.Logging;

public static class StructuredLog
{
	public static void Log(
		ILogger logger,
		LogLevel level,
		string eventName,
		string eventLayer,
		string eventDomain,
		string operationName,
		string outcome,
		string message,
		params (string Name, object? Value)[] properties)
	{
		Write(logger, level, exception: null, eventName, eventLayer, eventDomain, operationName, outcome, message, properties);
	}

	public static void Log(
		ILogger logger,
		LogLevel level,
		Exception exception,
		string eventName,
		string eventLayer,
		string eventDomain,
		string operationName,
		string outcome,
		string message,
		params (string Name, object? Value)[] properties)
	{
		Write(logger, level, exception, eventName, eventLayer, eventDomain, operationName, outcome, message, properties);
	}

	public static void Debug(ILogger logger, string eventName, string eventLayer, string eventDomain, string operationName, string outcome, string message, params (string Name, object? Value)[] properties) =>
		Log(logger, LogLevel.Debug, eventName, eventLayer, eventDomain, operationName, outcome, message, properties);

	public static void Info(ILogger logger, string eventName, string eventLayer, string eventDomain, string operationName, string outcome, string message, params (string Name, object? Value)[] properties) =>
		Log(logger, LogLevel.Information, eventName, eventLayer, eventDomain, operationName, outcome, message, properties);

	public static void Warning(ILogger logger, string eventName, string eventLayer, string eventDomain, string operationName, string outcome, string message, params (string Name, object? Value)[] properties) =>
		Log(logger, LogLevel.Warning, eventName, eventLayer, eventDomain, operationName, outcome, message, properties);

	public static void Error(ILogger logger, Exception exception, string eventName, string eventLayer, string eventDomain, string operationName, string outcome, string message, params (string Name, object? Value)[] properties) =>
		Log(logger, LogLevel.Error, exception, eventName, eventLayer, eventDomain, operationName, outcome, message, properties);

	public static (string Name, object? Value)[] Combine(
		ReadOnlySpan<(string Name, object? Value)> first,
		params (string Name, object? Value)[] second)
	{
		var result = new (string Name, object? Value)[first.Length + second.Length];
		for(var i = 0; i < first.Length; i++)
		{
			result[i] = first[i];
		}

		for(var i = 0; i < second.Length; i++)
		{
			result[first.Length + i] = second[i];
		}

		return result;
	}

	private static void Write(
		ILogger logger,
		LogLevel level,
		Exception? exception,
		string eventName,
		string eventLayer,
		string eventDomain,
		string operationName,
		string outcome,
		string message,
		params (string Name, object? Value)[] properties)
	{
		if(logger is null)
		{
			throw new ArgumentNullException(nameof(logger));
		}

		if(!logger.IsEnabled(level))
		{
			return;
		}

		var state = new StructuredLogState(
			NormalizeRequired(eventName, "unknown"),
			NormalizeRequired(eventLayer, "unknown"),
			NormalizeRequired(eventDomain, StructuredLogDomains.Unknown),
			NormalizeRequired(operationName, "unknown"),
			NormalizeRequired(outcome, "unknown"),
			NormalizeMessage(message),
			properties);

		logger.Log(level, default(EventId), state, exception, static (s, _) => s.Message);
	}

	private static string NormalizeRequired(string? value, string fallback)
	{
		var normalized = (value ?? string.Empty).Trim();
		return normalized.Length == 0
			? fallback
			: normalized;
	}

	private static string NormalizeMessage(string? value)
	{
		var normalized = (value ?? string.Empty).Trim();
		return normalized.Length == 0
			? "Structured log message."
			: normalized;
	}

	private sealed class StructuredLogState : IReadOnlyList<KeyValuePair<string, object?>>
	{
		private readonly KeyValuePair<string, object?>[] _items;

		public StructuredLogState(
			string eventName,
			string eventLayer,
			string eventDomain,
			string operationName,
			string outcome,
			string message,
			IReadOnlyList<(string Name, object? Value)> properties)
		{
			Message = message;
			var normalizedProperties = NormalizeProperties(properties);

			_items = new KeyValuePair<string, object?>[7 + normalizedProperties.Count];
			_items[0] = new KeyValuePair<string, object?>("event.name", eventName);
			_items[1] = new KeyValuePair<string, object?>("event.layer", eventLayer);
			_items[2] = new KeyValuePair<string, object?>("event.domain", eventDomain);
			_items[3] = new KeyValuePair<string, object?>("operation.name", operationName);
			_items[4] = new KeyValuePair<string, object?>("event.outcome", outcome);
			_items[5] = new KeyValuePair<string, object?>("message", Message);

			for(var i = 0; i < normalizedProperties.Count; i++)
			{
				_items[6 + i] = new KeyValuePair<string, object?>(
					normalizedProperties[i].Name,
					normalizedProperties[i].Value);
			}

			_items[^1] = new KeyValuePair<string, object?>("{OriginalFormat}", Message);
		}

		public string Message { get; }
		public int Count => _items.Length;
		public KeyValuePair<string, object?> this[int index] => _items[index];
		public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() => ((IEnumerable<KeyValuePair<string, object?>>)_items).GetEnumerator();
		IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();

		private static List<(string Name, object? Value)> NormalizeProperties(IReadOnlyList<(string Name, object? Value)> properties)
		{
			var result = new List<(string Name, object? Value)>(properties.Count);

			for(var i = 0; i < properties.Count; i++)
			{
				var (name, value) = properties[i];
				var normalizedName = (name ?? string.Empty).Trim();
				if(normalizedName.Length == 0)
				{
					continue;
				}

				result.Add((normalizedName, value));
			}

			return result;
		}
	}
}

public static class StructuredLogDomains
{
	public const string Unknown = "unknown";
	public const string Startup = "startup";
	public const string Http = "http";
	public const string Api = "api";
	public const string Geo = "geo";
	public const string Image = "images";
	public const string Task = "tasks";
	public const string User = "users";
	public const string UserPoints = "user_points";
	public const string Otp = "otp";
	public const string PasswordReset = "password_reset";
}

public static class StructuredLogOutcomes
{
	public const string Started = "started";
	public const string Completed = "completed";
	public const string Rejected = "rejected";
	public const string Failed = "failed";
	public const string Aborted = "aborted";
}