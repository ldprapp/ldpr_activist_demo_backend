using LdprActivistDemo.Api.Health;
using LdprActivistDemo.Api.Middleware;
using LdprActivistDemo.Application;
using LdprActivistDemo.Application.Images;
using LdprActivistDemo.Application.Otp;
using LdprActivistDemo.Application.PasswordReset;
using LdprActivistDemo.Application.Users;
using LdprActivistDemo.Persistence;
using LdprActivistDemo.Persistence.Repositories;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
	.AddJsonOptions(o =>
	{
		o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
	});

builder.Services.Configure<ApiBehaviorOptions>(o => o.SuppressModelStateInvalidFilter = true);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<IBackendVersionProvider, BackendVersionProvider>();

builder.Services.Configure<OtpOptions>(builder.Configuration.GetSection("Otp"));
builder.Services.Configure<PasswordResetOptions>(builder.Configuration.GetSection("PasswordReset"));
builder.Services.AddApplication();
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddScoped<IImageRepository, ImageRepository>();

var app = builder.Build();

await using(var scope = app.Services.CreateAsyncScope())
{
	var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
	var autoMigrate = builder.Configuration.GetValue<bool>("Database:AutoMigrate");

	if(autoMigrate)
	{
		await db.Database.MigrateAsync(app.Lifetime.ApplicationStopping);
	}

	var geoSeeder = scope.ServiceProvider.GetRequiredService<GeoDbSeeder>();
	await geoSeeder.SeedAsync(app.Lifetime.ApplicationStopping);

	var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
	await userRepository.DeleteAllUnconfirmedAsync(app.Lifetime.ApplicationStopping);
}

app.UseMiddleware<ApiExceptionHandlingMiddleware>();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionDetailsMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();
app.Run();
