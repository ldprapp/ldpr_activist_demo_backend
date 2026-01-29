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
					return text;
				}

				logger.LogWarning("Version file '{VersionFilePath}' is empty. Using '{DefaultVersion}'.",
					path,
					DefaultVersion);

				return DefaultVersion;
			}
			catch(Exception ex)
			{
				logger.LogWarning(ex, "Failed to read backend version file '{VersionFilePath}'.", path);
			}
		}

		logger.LogWarning("Backend version file not found. Tried: {Candidates}.",
			string.Join(", ", candidates));

		return DefaultVersion;
	}
}