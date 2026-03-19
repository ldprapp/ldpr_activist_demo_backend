using LdprActivistDemo.Api.Logging;
using LdprActivistDemo.Application.Diagnostics;

namespace LdprActivistDemo.Api.Health;

/// <summary>Читает версию бэкенда из файла <c>version</c> и кэширует её.</summary>
public sealed class BackendVersionProvider : IBackendVersionProvider
{
	private const string DefaultVersion = "unknown";

	private readonly string _version;

	public BackendVersionProvider(IHostEnvironment environment, ILogger<BackendVersionProvider> logger)
	{
		_version = LoadVersion(environment, logger);
	}

	public string Version => _version;

	private static string LoadVersion(IHostEnvironment environment, ILogger logger)
	{
		var candidates = new[]
		{
			Path.GetFullPath(Path.Combine(environment.ContentRootPath, "..", "version")),
			Path.GetFullPath(Path.Combine(environment.ContentRootPath, "version")),
			Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "version")),
			Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "version")),
		}
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToArray();

		var scopeProperties = new (string Name, object? Value)[]
		{
			("ContentRootPath", environment.ContentRootPath),
			("CandidateCount", candidates.Length),
		};

		using var scope = logger.BeginExecutionScope(
			DomainLogEvents.Startup.SequenceInitialized,
			LogLayers.Host,
			ApiLogOperations.Startup.ResolveVersion,
			scopeProperties);

		foreach(var path in candidates)
		{
			try
			{
				if(!File.Exists(path))
				{
					continue;
				}

				var text = (File.ReadAllText(path) ?? string.Empty).Trim();
				if(!string.IsNullOrWhiteSpace(text))
				{
					logger.LogCompleted(
						LogLevel.Information,
						DomainLogEvents.Startup.SequenceInitialized,
						LogLayers.Host,
						ApiLogOperations.Startup.ResolveVersion,
						"Backend version resolved from file.",
						("VersionFilePath", path),
						("Version", text));

					return text;
				}

				logger.LogRejected(
					LogLevel.Warning,
					DomainLogEvents.Startup.SequenceInitialized,
					LogLayers.Host,
					ApiLogOperations.Startup.ResolveVersion,
					"Backend version file is empty. Using default version.",
					("VersionFilePath", path),
					("DefaultVersion", DefaultVersion));

				return DefaultVersion;
			}
			catch(Exception ex)
			{
				logger.LogFailed(
					LogLevel.Warning,
					DomainLogEvents.Startup.SequenceInitialized,
					LogLayers.Host,
					ApiLogOperations.Startup.ResolveVersion,
					"Failed to read backend version file. Using next candidate.",
					ex,
					("VersionFilePath", path));
			}
		}

		logger.LogRejected(
			LogLevel.Warning,
			DomainLogEvents.Startup.SequenceInitialized,
			LogLayers.Host,
			ApiLogOperations.Startup.ResolveVersion,
			"Backend version file not found. Using default version.",
			("Candidates", string.Join(", ", candidates)),
			("DefaultVersion", DefaultVersion));

		return DefaultVersion;
	}
}