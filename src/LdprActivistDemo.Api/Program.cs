using LdprActivistDemo.Api.Health;
using LdprActivistDemo.Api.Logging;
using LdprActivistDemo.Api.Middleware;
using LdprActivistDemo.Api.Time;
using LdprActivistDemo.Application;
using LdprActivistDemo.Application.Diagnostics;
using LdprActivistDemo.Application.Otp;
using LdprActivistDemo.Application.PasswordReset;
using LdprActivistDemo.Application.Users;
using LdprActivistDemo.Persistence;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using Serilog;

StructuredLoggingBootstrapper.ConfigureBootstrapLogger();

Log.Information("Bootstrap logger configured.");

try
{
	var builder = WebApplication.CreateBuilder(args);

	builder.Services.Configure<ApplicationTimeOptions>(
		builder.Configuration.GetSection(ApplicationTimeOptions.SectionName));

	var applicationTimeOptions = builder.Configuration.GetSection(ApplicationTimeOptions.SectionName).Get<ApplicationTimeOptions>() ?? new ApplicationTimeOptions();
	var applicationCulture = ApplicationTimeCultureConfigurator.ApplyDefaultCulture(applicationTimeOptions.Locale);

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

	builder.Services.AddSingleton<IBackendVersionProvider, BackendVersionProvider>();
	builder.Services.Configure<OtpOptions>(builder.Configuration.GetSection("Otp"));
	builder.Services.Configure<PasswordResetOptions>(builder.Configuration.GetSection("PasswordReset"));
	builder.Services.Configure<SmsRuOptions>(builder.Configuration.GetSection(SmsRuOptions.SectionName));

	builder.Services.AddApplication();
	builder.Services.AddPersistence(builder.Configuration);
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

	app.UseMiddleware<CorrelationIdMiddleware>();
	app.UseMiddleware<ApiExceptionHandlingMiddleware>();
	app.UseMiddleware<RequestLoggingMiddleware>();
	app.UseSwagger();
	app.UseSwaggerUI();

	await using(var scope = app.Services.CreateAsyncScope())
	{
		var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
		var autoMigrate = builder.Configuration.GetValue<bool>("Database:AutoMigrate");
		var migrationProperties = new (string Name, object? Value)[]
		{
			("AutoMigrateEnabled", autoMigrate),
			("DbContextType", db.GetType().Name),
		};

		using(app.Logger.BeginExecutionScope(
				  DomainLogEvents.Startup.MigrationsApply,
				  LogLayers.Host,
				  ApiLogOperations.Startup.ApplyMigrations,
				  migrationProperties))
		{
			if(autoMigrate)
			{
				app.Logger.LogStarted(
					DomainLogEvents.Startup.MigrationsApply,
					LogLayers.Host,
					ApiLogOperations.Startup.ApplyMigrations,
					"Applying pending Entity Framework Core migrations.",
					migrationProperties);

				await db.Database.MigrateAsync(app.Lifetime.ApplicationStopping);

				app.Logger.LogCompleted(
					LogLevel.Information,
					DomainLogEvents.Startup.MigrationsApply,
					LogLayers.Host,
					ApiLogOperations.Startup.ApplyMigrations,
					"Entity Framework Core migrations applied successfully.",
					migrationProperties);
			}
			else
			{
				app.Logger.LogCompleted(
					LogLevel.Information,
					DomainLogEvents.Startup.MigrationsApply,
					LogLayers.Host,
					ApiLogOperations.Startup.ApplyMigrations,
					"Automatic Entity Framework Core migrations are disabled.",
					migrationProperties);
			}
		}

		var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
		var deletedCount = await userRepository.DeleteAllUnconfirmedAsync(app.Lifetime.ApplicationStopping);

		var cleanupProperties = new (string Name, object? Value)[]
		{
			("DeletedCount", deletedCount),
		};

		using(app.Logger.BeginExecutionScope(
				  DomainLogEvents.Startup.CleanupUnconfirmedUsers,
				  LogLayers.Host,
				  ApiLogOperations.Startup.CleanupUnconfirmedUsers,
				  cleanupProperties))
		{
			app.Logger.LogCompleted(
				LogLevel.Information,
				DomainLogEvents.Startup.CleanupUnconfirmedUsers,
				LogLayers.Host,
				ApiLogOperations.Startup.CleanupUnconfirmedUsers,
				"Startup cleanup of unconfirmed users completed.",
				cleanupProperties);
		}
	}

	app.MapControllers();

	var pipelineProperties = new (string Name, object? Value)[]
	{
		("EnvironmentName", app.Environment.EnvironmentName),
		("ApplicationTimeLocale", applicationCulture.Name),
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