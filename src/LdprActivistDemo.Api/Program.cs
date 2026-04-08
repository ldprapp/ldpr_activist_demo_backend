using LdprActivistDemo.Api.Cors;
using LdprActivistDemo.Api.Health;
using LdprActivistDemo.Api.Logging;
using LdprActivistDemo.Api.Middleware;
using LdprActivistDemo.Api.RateLimiting;
using LdprActivistDemo.Api.Time;
using LdprActivistDemo.Application;
using LdprActivistDemo.Application.Diagnostics;
using LdprActivistDemo.Application.Otp;
using LdprActivistDemo.Application.PasswordReset;
using LdprActivistDemo.Persistence;

using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

using Serilog;

StructuredLoggingBootstrapper.ConfigureBootstrapLogger();

Log.Information("Bootstrap logger configured.");

try
{
	const string FrontendCorsPolicyName = "Frontend";

	var builder = WebApplication.CreateBuilder(args);

	builder.Services.Configure<ApplicationTimeOptions>(
		builder.Configuration.GetSection(ApplicationTimeOptions.SectionName));

	var applicationTimeOptions = builder.Configuration.GetSection(ApplicationTimeOptions.SectionName).Get<ApplicationTimeOptions>() ?? new ApplicationTimeOptions();
	var applicationCulture = ApplicationTimeCultureConfigurator.ApplyDefaultCulture(applicationTimeOptions.Locale);
	var swaggerEnabled = builder.Configuration.GetValue<bool>("Swagger:Enabled");
	var rateLimitingOptions = builder.Configuration
		.GetSection(ApiRateLimitingOptions.SectionName)
		.Get<ApiRateLimitingOptions>() ?? new ApiRateLimitingOptions();

	builder.Services.Configure<ForwardedHeadersOptions>(options =>
	{
		options.ForwardedHeaders =
			ForwardedHeaders.XForwardedFor
			| ForwardedHeaders.XForwardedProto
			| ForwardedHeaders.XForwardedHost;

		// API is no longer published directly to the host.
		// Requests should arrive only from the local nginx reverse proxy inside docker network.
		options.KnownNetworks.Clear();
		options.KnownProxies.Clear();
	});

	var configuredCorsOrigins =
		builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
		?? Array.Empty<string>();
	var allowDevelopmentLoopbackOrigins =
		builder.Configuration.GetValue<bool>("Cors:AllowDevelopmentLoopbackOrigins");

	builder.Services.AddCors(options =>
	{
		options.AddPolicy(FrontendCorsPolicyName, policy => policy
			.SetIsOriginAllowed(origin => CorsOriginMatcher.IsAllowed(
				origin,
				configuredCorsOrigins,
				builder.Environment.IsDevelopment() && allowDevelopmentLoopbackOrigins))
			.AllowAnyHeader()
			.AllowAnyMethod()
			.WithExposedHeaders(CorrelationIdMiddleware.HeaderName));
	});

	builder.AddStructuredLogging();

	builder.Services.AddScoped<StructuredActionLoggingFilter>();
	builder.Services
		.AddControllers(options =>
		{
			options.Filters.AddService<StructuredActionLoggingFilter>();
		})
		.AddJsonOptions(o =>
		{
			o.JsonSerializerOptions.Converters.Add(
				new System.Text.Json.Serialization.JsonStringEnumConverter());
		});

	builder.Services.Configure<ApiBehaviorOptions>(o => o.SuppressModelStateInvalidFilter = true);
	if(swaggerEnabled)
	{
		builder.Services.AddEndpointsApiExplorer();
		builder.Services.AddSwaggerGen(o =>
		{
			var apiAssembly = typeof(Program).Assembly;
			var xmlFile = $"{apiAssembly.GetName().Name}.xml";
			var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
			if(File.Exists(xmlPath))
			{
				o.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
			}
		});
	}

	builder.Services.AddSingleton<IBackendVersionProvider, BackendVersionProvider>();
	builder.Services.Configure<OtpOptions>(builder.Configuration.GetSection("Otp"));
	builder.Services.Configure<PasswordResetOptions>(builder.Configuration.GetSection("PasswordReset"));
	builder.Services.Configure<SmsRuOptions>(builder.Configuration.GetSection(SmsRuOptions.SectionName));
	builder.Services.Configure<ApiRateLimitingOptions>(
		builder.Configuration.GetSection(ApiRateLimitingOptions.SectionName));

	builder.Services.AddApplication();
	builder.Services.AddPersistence(builder.Configuration);
	builder.Services.AddApiRateLimiting(rateLimitingOptions);
	builder.Services.AddHttpClient<IOtpSender, SmsRuOtpSender>((serviceProvider, httpClient) =>
	{
		var options = serviceProvider
			.GetRequiredService<IOptions<SmsRuOptions>>()
			.Value;

		var baseUrl = string.IsNullOrWhiteSpace(options.BaseUrl)
			? "https://sms.ru"
			: options.BaseUrl.Trim();

		httpClient.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
		httpClient.Timeout = TimeSpan.FromSeconds(15);
	});

	var app = builder.Build();

	app.Lifetime.ApplicationStarted.Register(() =>
	{
		var properties = new (string Name, object? Value)[]
		{
			("EnvironmentName", app.Environment.EnvironmentName),
			("ApplicationTimeLocale", applicationCulture.Name),
			("ContentRootPath", app.Environment.ContentRootPath),
		};

		using var scope = app.Logger.BeginExecutionScope(
			DomainLogEvents.Startup.ApplicationStarted,
			LogLayers.Host,
			ApiLogOperations.Host.ApplicationStarted,
			properties);

		app.Logger.LogCompleted(
			LogLevel.Information,
			DomainLogEvents.Startup.ApplicationStarted,
			LogLayers.Host,
			ApiLogOperations.Host.ApplicationStarted,
			"Application started and is ready to accept requests.",
			properties);
	});

	app.Lifetime.ApplicationStopping.Register(() =>
	{
		var properties = new (string Name, object? Value)[]
		{
			("EnvironmentName", app.Environment.EnvironmentName),
			("ApplicationTimeLocale", applicationCulture.Name),
		};

		using var scope = app.Logger.BeginExecutionScope(
			DomainLogEvents.Startup.ApplicationStopping,
			LogLayers.Host,
			ApiLogOperations.Host.ApplicationStopping,
			properties);

		app.Logger.LogStarted(
			DomainLogEvents.Startup.ApplicationStopping,
			LogLayers.Host,
			ApiLogOperations.Host.ApplicationStopping,
			"Application stopping sequence started.",
			properties);
	});

	var startupProperties = new (string Name, object? Value)[]
	{
		("EnvironmentName", app.Environment.EnvironmentName),
		("ApplicationTimeLocale", applicationCulture.Name),
		("ContentRootPath", app.Environment.ContentRootPath),
		("SwaggerEnabled", swaggerEnabled),
		("RateLimitingEnabled", rateLimitingOptions.Enabled),
	};

	using(var scope = app.Logger.BeginExecutionScope(
			  DomainLogEvents.Startup.SequenceInitialized,
			  LogLayers.Host,
			  ApiLogOperations.Startup.InitializeSequence,
			  startupProperties))
	{
		app.Logger.LogStarted(
			DomainLogEvents.Startup.SequenceInitialized,
			LogLayers.Host,
			ApiLogOperations.Startup.InitializeSequence,
			"Application startup sequence initialized.",
			startupProperties);
	}

	app.UseForwardedHeaders();
	app.UseRouting();
	app.UseMiddleware<CorrelationIdMiddleware>();
	app.UseMiddleware<ApiExceptionHandlingMiddleware>();
	app.UseMiddleware<RequestLoggingMiddleware>();
	app.UseCors(FrontendCorsPolicyName);
	if(rateLimitingOptions.Enabled)
	{
		app.UseRateLimiter();
	}
	app.UseMiddleware<ActorPasswordHeaderDecodingMiddleware>();
	if(swaggerEnabled)
	{
		app.UseSwagger();
		app.UseSwaggerUI();
	}

	var autoMigrate = builder.Configuration.GetValue<bool>("Database:AutoMigrate");
	var persistenceInitializationProperties = new (string Name, object? Value)[]
 	{
		("AutoMigrateEnabled", autoMigrate),
	};

	using(app.Logger.BeginExecutionScope(
			  DomainLogEvents.Startup.MigrationsApply,
			  LogLayers.Host,
			  ApiLogOperations.Startup.ApplyMigrations,
			  persistenceInitializationProperties))
	{
		app.Logger.LogStarted(
			DomainLogEvents.Startup.MigrationsApply,
			LogLayers.Host,
			ApiLogOperations.Startup.ApplyMigrations,
			"Initializing persistence startup sequence.",
			persistenceInitializationProperties);

		await app.Services.InitializePersistenceAsync(
			autoMigrate,
			app.Lifetime.ApplicationStopping);

		app.Logger.LogCompleted(
			LogLevel.Information,
			DomainLogEvents.Startup.MigrationsApply,
			LogLayers.Host,
			ApiLogOperations.Startup.ApplyMigrations,
			"Persistence startup sequence completed successfully.",
			persistenceInitializationProperties);
	}


	app.MapControllers();

	var pipelineProperties = new (string Name, object? Value)[]
	{
		("EnvironmentName", app.Environment.EnvironmentName),
		("ApplicationTimeLocale", applicationCulture.Name),
		("SwaggerEnabled", swaggerEnabled),
		("RateLimitingEnabled", rateLimitingOptions.Enabled),
	};

	using(var scope = app.Logger.BeginExecutionScope(
			  DomainLogEvents.Startup.PipelineReady,
			  LogLayers.Host,
			  ApiLogOperations.Startup.PipelineReady,
			  pipelineProperties))
	{
		app.Logger.LogCompleted(
			LogLevel.Information,
			DomainLogEvents.Startup.PipelineReady,
			LogLayers.Host,
			ApiLogOperations.Startup.PipelineReady,
			"HTTP pipeline configured. Starting web application.",
			pipelineProperties);
	}

	app.Run();
}
catch(Exception ex)
{
	Log.Fatal(ex, "Web application terminated unexpectedly during startup or execution.");
	throw;
}
finally
{
	await Log.CloseAndFlushAsync();
}