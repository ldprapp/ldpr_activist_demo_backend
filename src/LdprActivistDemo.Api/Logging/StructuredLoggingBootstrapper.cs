using System.Diagnostics;

using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace LdprActivistDemo.Api.Logging;

/// <summary>
/// Централизованная инициализация structured-logging для API.
/// </summary>
public static class StructuredLoggingBootstrapper
{
	private const string TextLogOutputTemplate =
		"[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{Level:u3}] {Message:lj}{NewLine}{Exception}";

	/// <summary>
	/// Настраивает bootstrap-логгер, чтобы не потерять ошибки раннего старта приложения.
	/// </summary>
	public static void ConfigureBootstrapLogger()
	{
		var environmentName =
			Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
			?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
			?? "Production";

		var contentRootPath = ResolveBootstrapContentRoot();

		var configuration = new ConfigurationBuilder()
			.SetBasePath(contentRootPath)
			.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
			.AddJsonFile($"appsettings.{environmentName}.json", optional: true, reloadOnChange: false)
			.AddEnvironmentVariables()
			.Build();

		var options = configuration
			.GetSection(StructuredLoggingOptions.SectionName)
			.Get<StructuredLoggingOptions>() ?? new StructuredLoggingOptions();

		var loggerConfiguration = new LoggerConfiguration();
		ConfigureLogger(
			loggerConfiguration,
			environmentName,
			contentRootPath,
			options);

		Log.Logger = loggerConfiguration.CreateBootstrapLogger();
	}

	/// <summary>
	/// Подключает основной Serilog pipeline к <see cref="WebApplicationBuilder"/>.
	/// </summary>
	/// <param name="builder">Текущий builder веб-приложения.</param>
	/// <returns>Тот же <see cref="WebApplicationBuilder"/>.</returns>
	public static WebApplicationBuilder AddStructuredLogging(this WebApplicationBuilder builder)
	{
		builder.Host.UseSerilog((context, _, loggerConfiguration) =>
		{
			var options = context.Configuration
				.GetSection(StructuredLoggingOptions.SectionName)
				.Get<StructuredLoggingOptions>() ?? new StructuredLoggingOptions();

			ConfigureLogger(
				loggerConfiguration,
				context.HostingEnvironment.EnvironmentName,
				context.HostingEnvironment.ContentRootPath,
				options);
		});

		return builder;
	}

	private static void ConfigureLogger(
		LoggerConfiguration loggerConfiguration,
		string environmentName,
		string contentRootPath,
		StructuredLoggingOptions options)
	{
		var serviceName = NormalizeServiceName(options.ServiceName);
		var serviceNamespace = NormalizeOptional(options.ServiceNamespace) ?? "LdprActivistDemo";
		var instanceId = NormalizeOptional(options.InstanceId)
			?? NormalizeOptional(Environment.GetEnvironmentVariable("HOSTNAME"))
			?? Environment.MachineName;
		var serviceVersion = ResolveServiceVersion(contentRootPath, options);

		loggerConfiguration
			.MinimumLevel.Is(ParseLevel(options.MinimumLevel.Default, LogEventLevel.Information))
			.MinimumLevel.Override("Microsoft", ParseLevel(options.MinimumLevel.Microsoft, LogEventLevel.Warning))
			.MinimumLevel.Override("Microsoft.AspNetCore", ParseLevel(options.MinimumLevel.MicrosoftAspNetCore, LogEventLevel.Warning))
			.MinimumLevel.Override("Microsoft.EntityFrameworkCore", ParseLevel(options.MinimumLevel.MicrosoftEntityFrameworkCore, LogEventLevel.Information))
			.MinimumLevel.Override("System", ParseLevel(options.MinimumLevel.System, LogEventLevel.Warning))
			.Enrich.FromLogContext()
			.Enrich.With(new LogStorageRoutingEnricher())
			.Enrich.With(new ActivityIdentifiersEnricher())
			.Enrich.WithProperty("service.name", serviceName)
			.Enrich.WithProperty("service.namespace", serviceNamespace)
			.Enrich.WithProperty("service.instance.id", instanceId)
			.Enrich.WithProperty("deployment.environment", environmentName);

		if(!string.IsNullOrWhiteSpace(serviceVersion))
		{
			loggerConfiguration.Enrich.WithProperty("service.version", serviceVersion);
		}

		if(options.Console.Enabled)
		{
			loggerConfiguration.WriteTo.Console(
				outputTemplate: TextLogOutputTemplate);
		}

		if(options.Files.Enabled)
		{
			ConfigureFileSinks(loggerConfiguration, options.Files);
		}
	}

	private static void ConfigureFileSinks(
		LoggerConfiguration loggerConfiguration,
		StructuredLoggingFilesOptions options)
	{
		var rootPath = ResolveRootPath(options.RootPath);
		Directory.CreateDirectory(rootPath);

		long? fileSizeLimitBytes = options.FileSizeLimitBytes > 0
			? options.FileSizeLimitBytes
			: null;
		int? retainedFileCountLimit = options.RetainedFileCountLimit > 0
			? options.RetainedFileCountLimit
			: null;
		var rollOnFileSizeLimit = fileSizeLimitBytes.HasValue && options.RollOnFileSizeLimit;
		var dateSinkMapCountLimit = options.DateSinkMapCountLimit <= 0
			? 0
			: options.DateSinkMapCountLimit;
		var levelSinkMapCountLimit = options.LevelSinkMapCountLimit <= 0
			? 0
			: options.LevelSinkMapCountLimit;

		loggerConfiguration.WriteTo.Map(
			"LogDate",
			"unknown-date",
			(logDate, dateWriteTo) => dateWriteTo.Map(
				"LogLevel",
				"unknown",
				(logLevel, levelWriteTo) => levelWriteTo.File(
					path: Path.Combine(
						rootPath,
						SanitizePathSegment(logDate),
						$"{SanitizePathSegment(logLevel)}.json"),
					outputTemplate: TextLogOutputTemplate,
					shared: options.Shared,
					fileSizeLimitBytes: fileSizeLimitBytes,
					rollOnFileSizeLimit: rollOnFileSizeLimit,
					retainedFileCountLimit: retainedFileCountLimit),
				sinkMapCountLimit: levelSinkMapCountLimit),
			sinkMapCountLimit: dateSinkMapCountLimit);
	}

	private static string ResolveBootstrapContentRoot()
	{
		var currentDirectory = Directory.GetCurrentDirectory();
		var baseDirectory = AppContext.BaseDirectory;

		var candidates = new[]
		{
			currentDirectory,
			baseDirectory,
			Path.GetFullPath(Path.Combine(currentDirectory, "src", "LdprActivistDemo.Api")),
			Path.GetFullPath(Path.Combine(baseDirectory, "src", "LdprActivistDemo.Api")),
		}
		.Distinct(StringComparer.OrdinalIgnoreCase)
		.ToArray();

		for(var i = 0; i < candidates.Length; i++)
		{
			if(File.Exists(Path.Combine(candidates[i], "appsettings.json")))
			{
				return candidates[i];
			}
		}

		return currentDirectory;
	}

	private static LogEventLevel ParseLevel(string? raw, LogEventLevel fallback)
		=> Enum.TryParse<LogEventLevel>(raw, ignoreCase: true, out var level)
			? level
			: fallback;

	private static string NormalizeServiceName(string? value)
	{
		var normalized = NormalizeOptional(value);
		return normalized ?? "ldpr-activist-demo.api";
	}

	private static string? NormalizeOptional(string? value)
	{
		var normalized = (value ?? string.Empty).Trim();
		return normalized.Length == 0 ? null : normalized;
	}

	private static string ResolveRootPath(string? value)
	{
		var normalized = NormalizeOptional(value) ?? "logs";
		return Path.IsPathRooted(normalized)
			? normalized
			: Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, normalized));
	}

	private static string SanitizePathSegment(string? value)
	{
		var normalized = NormalizeOptional(value) ?? "unknown";
		var invalidChars = Path.GetInvalidFileNameChars();

		var chars = normalized
			.Select(ch => invalidChars.Contains(ch) ? '_' : ch)
			.ToArray();

		return new string(chars);
	}

	private static string? ResolveServiceVersion(string contentRootPath, StructuredLoggingOptions options)
	{
		var configuredVersion = NormalizeOptional(options.ServiceVersion);
		if(configuredVersion is not null)
		{
			return configuredVersion;
		}

		var candidates = new[]
		{
			Path.GetFullPath(Path.Combine(contentRootPath, "..", "version")),
			Path.GetFullPath(Path.Combine(contentRootPath, "version")),
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
				if(text.Length > 0)
				{
					return text;
				}
			}
			catch
			{
				// Игнорируем ошибку чтения version-файла при конфигурации логирования.
			}
		}

		return null;
	}

	private sealed class LogStorageRoutingEnricher : ILogEventEnricher
	{
		public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
		{
			var logDate = logEvent.Timestamp.UtcDateTime.ToString("yyyy-MM-dd");
			var logLevel = logEvent.Level.ToString().ToLowerInvariant();

			logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("LogDate", logDate));
			logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("LogLevel", logLevel));
		}
	}

	private sealed class ActivityIdentifiersEnricher : ILogEventEnricher
	{
		public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
		{
			var activity = Activity.Current;
			if(activity is null)
			{
				return;
			}

			if(activity.TraceId != default)
			{
				logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("TraceId", activity.TraceId.ToHexString()));
			}

			if(activity.SpanId != default)
			{
				logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SpanId", activity.SpanId.ToHexString()));
			}

			if(activity.ParentSpanId != default)
			{
				logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ParentSpanId", activity.ParentSpanId.ToHexString()));
			}
		}
	}
}

public sealed class StructuredLoggingOptions
{
	public const string SectionName = "StructuredLogging";

	public string ServiceName { get; set; } = "ldpr-activist-demo.api";
	public string? ServiceNamespace { get; set; } = "LdprActivistDemo";
	public string? InstanceId { get; set; }
	public string? ServiceVersion { get; set; }
	public StructuredLoggingMinimumLevelOptions MinimumLevel { get; set; } = new();
	public StructuredLoggingConsoleOptions Console { get; set; } = new();
	public StructuredLoggingFilesOptions Files { get; set; } = new();
}

public sealed class StructuredLoggingMinimumLevelOptions
{
	public string Default { get; set; } = "Information";
	public string Microsoft { get; set; } = "Warning";
	public string MicrosoftAspNetCore { get; set; } = "Warning";
	public string MicrosoftEntityFrameworkCore { get; set; } = "Information";
	public string System { get; set; } = "Warning";
}

public sealed class StructuredLoggingConsoleOptions
{
	public bool Enabled { get; set; } = true;
}

public sealed class StructuredLoggingFilesOptions
{
	public bool Enabled { get; set; }
	public string RootPath { get; set; } = "logs";
	public bool Shared { get; set; } = true;
	public long FileSizeLimitBytes { get; set; } = 52_428_800;
	public bool RollOnFileSizeLimit { get; set; } = true;
	public int RetainedFileCountLimit { get; set; } = 30;
	public int DateSinkMapCountLimit { get; set; } = 14;
	public int LevelSinkMapCountLimit { get; set; } = 8;
}