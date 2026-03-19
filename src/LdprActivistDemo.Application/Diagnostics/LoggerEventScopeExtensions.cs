using LdprActivistDemo.Application.Logging;

using Microsoft.Extensions.Logging;

namespace LdprActivistDemo.Application.Diagnostics;

public static class LoggerEventScopeExtensions
{
	public static IDisposable BeginEventScope(
		this ILogger logger,
		string eventName,
		string layer,
		string? operationName = null)
	{
		ArgumentNullException.ThrowIfNull(logger);

		return BeginExecutionScopeCore(
			logger,
			eventName,
			layer,
			ExtractEventDomain(eventName),
			operationName,
			Array.Empty<(string Name, object? Value)>());
	}

	public static IDisposable BeginOperationScope(
		this ILogger logger,
		string eventName,
		string layer,
		string? operationName = null,
		params (string Name, object? Value)[] properties)
	{
		return logger.BeginExecutionScope(eventName, layer, operationName, properties);
	}

	public static IDisposable BeginExecutionScope(
		this ILogger logger,
		string eventName,
		string layer,
		string? operationName = null,
		params (string Name, object? Value)[] properties)
	{
		ArgumentNullException.ThrowIfNull(logger);

		return BeginExecutionScopeCore(
			logger,
			eventName,
			layer,
			ExtractEventDomain(eventName),
			operationName,
			properties);
	}

	public static IDisposable BeginLayerScope(
		this ILogger logger,
		string layer,
		string? domain = null,
		string? operationName = null,
		params (string Name, object? Value)[] properties)
	{
		ArgumentNullException.ThrowIfNull(logger);

		return BeginExecutionScopeCore(
			logger,
			null,
			layer,
			domain,
			operationName,
			properties);
	}

	private static IDisposable BeginExecutionScopeCore(
		ILogger logger,
		string? eventName,
		string layer,
		string? domain,
		string? operationName,
		params (string Name, object? Value)[] properties)
	{
		ArgumentNullException.ThrowIfNull(logger);

		var values = new Dictionary<string, object?>(StringComparer.Ordinal)
		{
			["event.layer"] = NormalizeValue(layer, "unknown"),
		};

		if(!string.IsNullOrWhiteSpace(eventName))
		{
			values["event.name"] = eventName.Trim();
		}

		if(!string.IsNullOrWhiteSpace(domain))
		{
			values["event.domain"] = domain.Trim();
		}

		if(!string.IsNullOrWhiteSpace(operationName))
		{
			values["operation.name"] = operationName.Trim();
		}

		var contextScope = logger.BeginScope(values) ?? EmptyScope.Instance;
		var propertyScope = logger.BeginPropertyScope(properties);
		return new CombinedScope(contextScope, propertyScope);
	}

	public static IDisposable BeginPropertyScope(
		this ILogger logger,
		params (string Name, object? Value)[] properties)
	{
		ArgumentNullException.ThrowIfNull(logger);

		if(properties is null || properties.Length == 0)
		{
			return EmptyScope.Instance;
		}

		var values = new Dictionary<string, object?>(StringComparer.Ordinal);
		for(var i = 0; i < properties.Length; i++)
		{
			var (name, value) = properties[i];
			if(string.IsNullOrWhiteSpace(name))
			{
				continue;
			}

			values[name.Trim()] = value;
		}

		return values.Count == 0 ? EmptyScope.Instance : logger.BeginScope(values) ?? EmptyScope.Instance;
	}

	public static string? ExtractEventDomain(string? eventName)
	{
		var normalized = (eventName ?? string.Empty).Trim();
		if(normalized.Length == 0)
		{
			return null;
		}

		var dotIndex = normalized.IndexOf('.');
		if(dotIndex > 0)
		{
			return normalized[..dotIndex];
		}

		var underscoreIndex = normalized.IndexOf('_');
		if(underscoreIndex > 0)
		{
			return normalized[..underscoreIndex];
		}

		return normalized;
	}

	public static void LogOutcome(
		this ILogger logger,
		LogLevel level,
		string eventName,
		string layer,
		string operationName,
		string outcome,
		string message,
		params (string Name, object? Value)[] properties)
	{
		var domain = ExtractEventDomain(eventName) ?? StructuredLogDomains.Unknown;

		StructuredLog.Log(
			logger,
			level,
			eventName,
			layer,
			domain,
			operationName,
			outcome,
			message,
			properties);
	}

	public static void LogStarted(
		this ILogger logger,
		string eventName,
		string layer,
		string operationName,
		string message,
		params (string Name, object? Value)[] properties) =>
		logger.LogOutcome(
			LogLevel.Debug,
			eventName,
			layer,
			operationName,
			StructuredLogOutcomes.Started,
			message,
			properties);

	public static void LogCompleted(
		this ILogger logger,
		LogLevel level,
		string eventName,
		string layer,
		string operationName,
		string message,
		params (string Name, object? Value)[] properties) =>
		logger.LogOutcome(
			level,
			eventName,
			layer,
			operationName,
			StructuredLogOutcomes.Completed,
			message,
			properties);

	public static void LogRejected(
		this ILogger logger,
		LogLevel level,
		string eventName,
		string layer,
		string operationName,
		string message,
		params (string Name, object? Value)[] properties) =>
		logger.LogOutcome(
			level,
			eventName,
			layer,
			operationName,
			StructuredLogOutcomes.Rejected,
			message,
			properties);

	public static void LogAborted(
		this ILogger logger,
		LogLevel level,
		string eventName,
		string layer,
		string operationName,
		string message,
		params (string Name, object? Value)[] properties)
		=> logger.LogOutcome(
			level,
			eventName,
			layer,
			operationName,
			StructuredLogOutcomes.Aborted,
			message,
			properties);

	public static void LogFailed(
		this ILogger logger,
		LogLevel level,
		string eventName,
		string layer,
		string operationName,
		string message,
		Exception exception,
		params (string Name, object? Value)[] properties)
	{
		ArgumentNullException.ThrowIfNull(logger);
		ArgumentNullException.ThrowIfNull(exception);

		var domain = ExtractEventDomain(eventName) ?? StructuredLogDomains.Unknown;

		StructuredLog.Log(
			logger,
			level,
			exception,
			eventName,
			layer,
			domain,
			operationName,
			StructuredLogOutcomes.Failed,
			message,
			properties);
	}

	private static string NormalizeValue(string? value, string fallback)
	{
		var normalized = (value ?? string.Empty).Trim();
		return normalized.Length == 0
			? fallback
			: normalized;
	}

	private sealed class CombinedScope : IDisposable
	{
		private readonly IDisposable _first;
		private readonly IDisposable _second;

		public CombinedScope(IDisposable first, IDisposable second)
		{
			_first = first;
			_second = second;
		}

		public void Dispose()
		{
			_second.Dispose();
			_first.Dispose();
		}
	}

	private sealed class EmptyScope : IDisposable
	{
		public static readonly EmptyScope Instance = new();

		private EmptyScope()
		{
		}

		public void Dispose()
		{
		}
	}
}